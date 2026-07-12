using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ItemInspectionController : MonoBehaviour
{
    public static ItemInspectionController Instance { get; private set; }

    [Header("Impostazioni")]
    public float          inspectionDistance = 1.5f;
    public float          normalizedSize     = 0.8f;
    public float          rotationSpeed      = 120f;
    public Key            toggleKey          = Key.Tab;
    public Key            dropKey            = Key.R;
    public TMP_FontAsset  uiFont;

    [Tooltip("Logga in Console la dimensione calcolata per ogni modello dell'inventario (per diagnosticare modelli microscopici/giganti).")]
    public bool           debugInspectionSizing = false;

    [Header("Carousel 3D")]
    public float carouselSpacing      = 1.2f;   // distanza orizzontale tra modelli (unità mondo)
    public float carouselScaleFalloff = 0.55f;  // fattore di scala per ogni step dal centro
    public float carouselYOffset      = 0.1f;   // offset verticale di tutti i modelli

    [Header("Carousel – Animazione e Scurimento")]
    [Min(0f)]          public float scaleAnimSpeed = 8f;
    [Range(0f, 1f)]    public float dimBrightness  = 0.3f; // 0 = nero, 1 = nessun effetto

    [Header("Info bar")]
    public Vector2 infoBarAnchorMin  = new Vector2(0f, 0f);
    public Vector2 infoBarAnchorMax  = new Vector2(1f, 0.18f);
    public Sprite  infoBarSprite;
    public Color   infoBarColor      = new Color(0f, 0f, 0f, 0.88f);
    public Vector2 nameAnchorMin    = new Vector2(0.05f, 0.55f);
    public Vector2 nameAnchorMax    = new Vector2(0.95f, 1f);
    public Vector2 descAnchorMin    = new Vector2(0.05f, 0.22f);
    public Vector2 descAnchorMax    = new Vector2(0.95f, 0.55f);
    public Vector2 hintAnchorMin    = new Vector2(0.05f, 0f);
    public Vector2 hintAnchorMax    = new Vector2(0.95f, 0.22f);
    [Min(1f)] public float nameFontSize        = 30f;
    [Min(1f)] public float descriptionFontSize = 18f;
    [Min(1f)] public float hintFontSize        = 14f;
    public string navigateHintText = "[A D] Naviga";
    public string rotateHintText   = "[LMB] Ruota";
    public string dropHintText     = "[R] Lascia";
    public string closeHintText    = "[E] Chiudi";

    [Header("Inventario vuoto")]
    public string emptyTitleText = "Inventario";
    public string emptyDescText  = "Vuoto";

    public bool IsInspecting => isInspecting;

    private bool              isInspecting;
    private int               currentIndex;
    private PhysicalInventory physicalInventory;

    private Camera        mainCamera;
    private Camera        overlayCamera;
    private FPSController playerController;
    private int           inspectionLayer = -1;

    private Canvas        overlayCanvas;
    private RenderTexture overlayTexture;
    private RawImage      modelRenderImage;
    private Light         overlayLight;
    private int           overlayTextureWidth;
    private int           overlayTextureHeight;

    private TextMeshProUGUI nameText;
    private TextMeshProUGUI descText;
    private TextMeshProUGUI hintText;

    private float[] scaleAnim; // scala animata per ogni entry, parallelo a carousel

    // Ogni voce del carousel: outer gestisce posizione/scala, inner gestisce normalizzazione
    private struct CarouselEntry
    {
        public CollectedItem         Item;
        public GameObject            Outer;
        public InspectableItemAction Action;
    }
    private readonly List<CarouselEntry> carousel = new List<CarouselEntry>();

    private GameObject            currentModel;
    private InspectableItemAction currentAction;
    private CollectedItem         currentItem;
    private Mesh                  bakeScratch; // mesh riusata per il bake delle skinned mesh

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ReleaseOverlayTexture();
        if (bakeScratch != null) Destroy(bakeScratch);
        if (physicalInventory != null)
        {
            physicalInventory.OnItemAdded   -= HandleInventoryChanged;
            physicalInventory.OnItemRemoved -= HandleInventoryChanged;
        }
    }

    void Start()
    {
        playerController = FindFirstObjectByType<FPSController>();
        mainCamera       = FindMainCamera();

        if (mainCamera == null)
        {
            Debug.LogError("[Inspection] Main Camera non trovata!");
            return;
        }

        inspectionLayer = ResolveInspectionLayer();
        SetupOverlayCamera();
        BuildUI();

        physicalInventory = PhysicalInventory.Instance;
        if (physicalInventory != null)
        {
            physicalInventory.OnItemAdded   += HandleInventoryChanged;
            physicalInventory.OnItemRemoved += HandleInventoryChanged;
        }

    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[toggleKey].wasPressedThisFrame)
        {
            if (isInspecting) CloseInspection();
            else              OpenInventory();
            return;
        }

        if (!isInspecting) return;

        EnsureOverlayTexture();
        if (currentAction != null) currentAction.Tick(Time.deltaTime);
        AnimateCarouselScales();

        if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
        {
            CloseInspection();
            return;
        }

        if (currentAction != null && currentAction.WasActionPressed(kb))
        {
            currentAction.Toggle();
            return;
        }

        if (CanUpgradeCurrent()
            && currentItem.upgradePath.actionKey != Key.None
            && kb[currentItem.upgradePath.actionKey].wasPressedThisFrame)
        {
            UpgradeCurrentItem();
            return;
        }

        if (kb[dropKey].wasPressedThisFrame)
        {
            DropCurrentItem();
            return;
        }

        if (carousel.Count > 1)
        {
            if (kb.aKey.wasPressedThisFrame)
                SwitchToIndex((currentIndex - 1 + carousel.Count) % carousel.Count);
            else if (kb.dKey.wasPressedThisFrame)
                SwitchToIndex((currentIndex + 1) % carousel.Count);
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed && currentModel != null)
        {
            Vector2   delta = mouse.delta.ReadValue();
            Transform cam   = overlayCamera != null ? overlayCamera.transform : mainCamera.transform;
            currentModel.transform.Rotate(cam.up,    -delta.x * rotationSpeed * Time.deltaTime, Space.World);
            currentModel.transform.Rotate(cam.right,  delta.y * rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    // ── public API ────────────────────────────────────────────────────────────

    public void OpenInventory()
    {
        if (isInspecting) return;

        int count    = GetItems().Count;
        currentIndex = count > 0 ? Mathf.Clamp(currentIndex, 0, count - 1) : 0;
        FreezePlayer(true);
        EnsureOverlayTexture();
        overlayCamera.enabled = true;
        if (overlayLight  != null) overlayLight.enabled = true;
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(true);
        RebuildCarouselModels();
        isInspecting = true;
    }

    public void OpenInspection(CollectedItem item)
    {
        var items = GetItems();
        for (int i = 0; i < items.Count; i++)
            if (items[i] == item) { currentIndex = i; break; }
        if (isInspecting) PositionCarouselModels();
        else              OpenInventory();
    }

    public void CloseInspection()
    {
        isInspecting  = false;
        currentModel  = null;
        currentAction = null;
        currentItem   = null;

        foreach (var e in carousel)
            if (e.Outer != null) Destroy(e.Outer);
        carousel.Clear();
        scaleAnim = null;

        if (overlayCamera != null) overlayCamera.enabled = false;
        if (overlayLight  != null) overlayLight.enabled  = false;
        if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(false);
        FreezePlayer(false);
    }

    // ── carousel 3D ───────────────────────────────────────────────────────────

    private void SwitchToIndex(int index)
    {
        if (index < 0 || index >= carousel.Count) return;
        currentIndex = index;
        PositionCarouselModels();
    }

    private void RebuildCarouselModels()
    {
        foreach (var e in carousel)
            if (e.Outer != null) Destroy(e.Outer);
        carousel.Clear();
        scaleAnim = null;

        var items = GetItems();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item?.worldSource == null)
            {
                carousel.Add(new CarouselEntry { Item = item });
                continue;
            }
            carousel.Add(BuildCarouselEntry(item));
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, carousel.Count - 1));
        PositionCarouselModels();
    }

    private void PositionCarouselModels()
    {
        // Inizializza scaleAnim solo se è la prima volta (rebuild): snap immediato alle scale target
        bool freshAnim = (scaleAnim == null || scaleAnim.Length != carousel.Count);
        if (freshAnim)
        {
            scaleAnim = new float[carousel.Count];
            for (int i = 0; i < carousel.Count; i++)
                scaleAnim[i] = Mathf.Pow(carouselScaleFalloff, Mathf.Abs(i - currentIndex));
        }

        for (int i = 0; i < carousel.Count; i++)
        {
            var outer = carousel[i].Outer;
            if (outer == null) continue;

            int offset = i - currentIndex;
            outer.transform.localPosition = new Vector3(offset * carouselSpacing, carouselYOffset, inspectionDistance);

            // Al primo posizionamento applica scala immediatamente; poi Update() la anima
            if (freshAnim)
                outer.transform.localScale = Vector3.one * scaleAnim[i];
        }

        currentModel  = currentIndex < carousel.Count ? carousel[currentIndex].Outer  : null;
        currentAction = currentIndex < carousel.Count ? carousel[currentIndex].Action : null;
        currentItem   = currentIndex < carousel.Count ? carousel[currentIndex].Item   : null;

        UpdateInfoBar();
        UpdateHintText();
        ApplyDimming();
    }

    private CarouselEntry BuildCarouselEntry(CollectedItem item)
    {
        // outer: gestisce posizione e scala nel carousel
        var outer = new GameObject("CarouselOuter_" + item.name);
        outer.transform.SetParent(overlayCamera.transform, false);
        outer.transform.localPosition = new Vector3(0f, carouselYOffset, inspectionDistance);
        outer.transform.localRotation = Quaternion.identity;
        outer.transform.localScale    = Vector3.one;

        // pivot: gestisce la normalizzazione (figlio di outer, localPos=zero)
        var pivot = new GameObject("InspectedItemRoot");
        pivot.transform.SetParent(outer.transform, false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;
        pivot.transform.localScale    = Vector3.one;

        var model = Instantiate(item.worldSource, pivot.transform, false);
        model.name = item.worldSource.name + "_Inspectable";
        model.SetActive(true);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        SetLayerRecursive(outer, inspectionLayer);
        NormalizeModelInPivot(pivot.transform, model.transform, item.inspectionScaleMultiplier);
        pivot.transform.localRotation = Quaternion.Euler(15f, -25f, 0f);

        var action = model.GetComponentInChildren<InspectableItemAction>(true);
        if (action != null) action.InitializeForInspection();

        return new CarouselEntry { Item = item, Outer = outer, Action = action };
    }

    private void AnimateCarouselScales()
    {
        if (scaleAnim == null) return;
        for (int i = 0; i < carousel.Count && i < scaleAnim.Length; i++)
        {
            if (carousel[i].Outer == null) continue;
            float target = Mathf.Pow(carouselScaleFalloff, Mathf.Abs(i - currentIndex));
            float next   = Mathf.Lerp(scaleAnim[i], target, Time.deltaTime * scaleAnimSpeed);
            if (Mathf.Abs(next - scaleAnim[i]) > 0.0005f)
            {
                scaleAnim[i] = next;
                carousel[i].Outer.transform.localScale = Vector3.one * next;
            }
        }
        // la scala anima ogni frame: il dimming è già applicato correttamente su ApplyDimming()
    }

    private void ApplyDimming()
    {
        for (int i = 0; i < carousel.Count; i++)
        {
            var outer = carousel[i].Outer;
            if (outer == null) continue;

            bool isCenter = (i == currentIndex);

            foreach (var r in outer.GetComponentsInChildren<Renderer>())
            {
                if (isCenter)
                {
                    r.SetPropertyBlock(null); // ripristina i colori originali del materiale
                }
                else
                {
                    var mpb = new MaterialPropertyBlock();
                    // Legge il colore base dal materiale condiviso e lo scurisce
                    Color original = Color.white;
                    var   mat      = r.sharedMaterial;
                    if (mat != null)
                    {
                        if      (mat.HasProperty("_BaseColor")) original = mat.GetColor("_BaseColor");
                        else if (mat.HasProperty("_Color"))     original = mat.GetColor("_Color");
                    }
                    Color dimmed = new Color(
                        original.r * dimBrightness,
                        original.g * dimBrightness,
                        original.b * dimBrightness,
                        original.a);
                    mpb.SetColor("_BaseColor", dimmed);
                    mpb.SetColor("_Color",     dimmed);
                    r.SetPropertyBlock(mpb);
                }
            }
        }
    }

    private void HandleInventoryChanged(CollectedItem _)
    {
        if (!isInspecting) return;
        var items = GetItems();
        currentIndex = items.Count == 0 ? 0 : Mathf.Clamp(currentIndex, 0, items.Count - 1);
        RebuildCarouselModels();
    }

    private void DropCurrentItem()
    {
        if (physicalInventory == null)
        {
            physicalInventory = PhysicalInventory.Instance;
        }

        var items = GetItems();
        if (physicalInventory == null || items.Count == 0 || currentIndex < 0 || currentIndex >= items.Count)
        {
            return;
        }

        CollectedItem item = items[currentIndex];
        if (item == null)
        {
            return;
        }

        GameObject droppedObject = PrepareDroppedObject(item);
        if (physicalInventory.RemoveItem(item, false) && droppedObject != null)
        {
            droppedObject.SetActive(true);
        }
    }

    private GameObject PrepareDroppedObject(CollectedItem item)
    {
        GameObject droppedObject = item.worldSource;
        if (droppedObject == null)
        {
            droppedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            droppedObject.name = item.name;
            droppedObject.transform.localScale = Vector3.one * 0.25f;
            item.worldSource = droppedObject;
        }

        droppedObject.name = string.IsNullOrWhiteSpace(item.name) ? "Oggetto lasciato" : item.name;
        SetLayerRecursive(droppedObject, 0);
        droppedObject.transform.SetParent(null, true);
        droppedObject.transform.position = GetDropPosition();
        droppedObject.transform.rotation = Quaternion.identity;

        PickupItem pickup = droppedObject.GetComponent<PickupItem>();
        if (pickup == null)
        {
            pickup = droppedObject.AddComponent<PickupItem>();
        }

        pickup.itemName = string.IsNullOrWhiteSpace(item.name) ? "Oggetto" : item.name;
        pickup.itemId = item.inventoryId;
        pickup.description = item.description;
        pickup.canInspect = item.canInspect;
        pickup.inspectionScaleMultiplier = item.inspectionScaleMultiplier;
        pickup.upgradePath = item.upgradePath;

        // La fisica va aggiunta dopo PickupItem: il suo Awake crea il collider di
        // raycast (assente nello snapshot), che serve al Rigidbody per appoggiarsi.
        EnsureDropPhysics(droppedObject, mainCamera != null ? mainCamera.transform : transform);

        return droppedObject;
    }

    // Configura la fisica dell'oggetto lasciato: cade con gravità e si appoggia sul
    // collider del pavimento invece di essere piazzato "incollato" a terra.
    private static void EnsureDropPhysics(GameObject droppedObject, Transform source)
    {
        // Un Rigidbody dinamico funziona solo con collider convex: le MeshCollider
        // create da PickupItem sono già convex, ma lo forziamo per sicurezza.
        foreach (var meshCol in droppedObject.GetComponentsInChildren<MeshCollider>())
            meshCol.convex = true;

        var body = droppedObject.GetComponent<Rigidbody>();
        if (body == null)
            body = droppedObject.AddComponent<Rigidbody>();

        body.isKinematic            = false;
        body.useGravity             = true;
        body.mass                   = 1f;
        body.interpolation          = RigidbodyInterpolation.Interpolate;
        // Continuous evita che oggetti piccoli attraversino il pavimento statico.
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Piccola spinta in avanti (solo orizzontale, così guardando in basso non
        // viene sparato dentro il pavimento) e un filo verso il basso.
        Vector3 forward = source != null
            ? Vector3.ProjectOnPlane(source.forward, Vector3.up)
            : Vector3.zero;
        Vector3 toss = forward.normalized * 1.0f + Vector3.down * 0.3f;
        body.linearVelocity  = toss;
        body.angularVelocity = Vector3.zero;
    }

    private Vector3 GetDropPosition()
    {
        Transform source = mainCamera != null ? mainCamera.transform : transform;
        Vector3 origin = source.position;

        // Usa solo la direzione orizzontale dello sguardo: guardando in basso, il
        // forward della camera punterebbe nel pavimento e l'oggetto comparirebbe
        // sotto il terreno (e cadrebbe fuori dal mondo).
        Vector3 forward = Vector3.ProjectOnPlane(source.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        forward.Normalize();

        // Distanza di rilascio davanti al giocatore, ridotta se c'è un muro vicino
        // così l'oggetto non compare oltre la parete.
        float distance = 0.8f;
        if (Physics.Raycast(origin, forward, out RaycastHit wall, distance + 0.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            distance = Mathf.Max(0.25f, wall.distance - 0.2f);
        }

        // Rilascia poco sotto la linea degli occhi, davanti al giocatore.
        Vector3 position = origin + forward * distance + Vector3.down * 0.35f;

        // Assicura che il punto sia sopra il pavimento: se sotto, alzalo appena
        // sopra il collider così la fisica lo fa appoggiare invece di sprofondare.
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = Mathf.Max(position.y, floor.point.y + 0.15f);
        }

        return position;
    }

    private void UpdateInfoBar()
    {
        var items = GetItems();
        if (items.Count == 0 || currentIndex >= items.Count)
        {
            if (nameText != null) nameText.text = emptyTitleText;
            if (descText != null) descText.text  = emptyDescText;
            return;
        }
        var cur = items[currentIndex];
        if (nameText != null) nameText.text = cur?.name ?? "";
        if (descText != null) descText.text  = cur?.description ?? "";
    }

    private void UpdateHintText()
    {
        if (hintText == null) return;
        if (GetItems().Count == 0)
        {
            hintText.text = closeHintText;
            return;
        }
        LogUpgradeEligibility();
        string actionHint = currentAction != null ? $"   {currentAction.HintText}" : "";
        string upgradeHint = CanUpgradeCurrent()
            ? $"   [{currentItem.upgradePath.actionKey}] {currentItem.upgradePath.actionLabel}"
            : "";
        hintText.text = $"{navigateHintText}   {rotateHintText}{actionHint}{upgradeHint}   {dropHintText}   {closeHintText}";
    }

    // ── potenziamento oggetto (es. inserire occhi nel peluche) ─────────────────

    // True se l'oggetto a fuoco ha uno stadio di potenziamento disponibile e il player
    // possiede l'oggetto richiesto per applicarlo.
    private bool CanUpgradeCurrent()
    {
        ItemUpgradePath path = currentItem?.upgradePath;
        if (path == null || !path.HasNext) return false;
        if (!path.Next.HasRequiredItem) return false;

        if (physicalInventory == null) physicalInventory = PhysicalInventory.Instance;
        return physicalInventory != null && physicalInventory.HasItem(path.Next.requiredItem);
    }

    // Spiega in Console perché il prompt di inserimento è (o non è) mostrato per l'oggetto
    // a fuoco. Attivare 'debugInspectionSizing' e aprire l'inventario sull'oggetto.
    private void LogUpgradeEligibility()
    {
        if (!debugInspectionSizing) return;
        ItemUpgradePath path = currentItem?.upgradePath;
        string req = (path != null && path.HasNext) ? path.Next.requiredItem : "-";
        bool hasReq = path != null && path.HasNext && path.Next.HasRequiredItem
                      && physicalInventory != null && physicalInventory.HasItem(req);
        Debug.Log($"[Inspection] upgrade item='{currentItem?.name}' hasPath={path != null} " +
                  $"stages={(path?.stages?.Length ?? 0)} nextIdx={(path?.nextStageIndex ?? -1)} " +
                  $"hasNext={(path != null && path.HasNext)} requiredItem='{req}' " +
                  $"inventoryHasIt={hasReq} => showPrompt={CanUpgradeCurrent()}");
    }

    private void UpgradeCurrentItem()
    {
        if (!CanUpgradeCurrent()) return;

        CollectedItem item = currentItem;
        ItemUpgradePath path = item.upgradePath;
        ItemUpgradeStage next = path.Next;

        // 1) Potenzia l'oggetto sul posto: nuovo modello, nome, descrizione. Lo stato
        //    (nextStageIndex) avanza sul CollectedItem, così segue l'oggetto.
        GameObject oldSource = item.worldSource;
        item.worldSource = InventorySnapshot.Create(next.model, next.name);
        if (!string.IsNullOrWhiteSpace(next.name)) item.name = next.name;
        item.description = next.description;
        item.canInspect = item.worldSource != null;
        item.inspectionScaleMultiplier = next.EffectiveInspectionScale;
        path.nextStageIndex++;
        if (oldSource != null) Destroy(oldSource);

        // 2) Consuma un oggetto richiesto (innesca un rebuild via HandleInventoryChanged).
        physicalInventory.RemoveItem(next.requiredItem);

        // 3) Mantieni il fuoco sull'oggetto potenziato e ridisegna il carosello.
        int idx = IndexOfItem(item);
        if (idx >= 0) currentIndex = idx;
        RebuildCarouselModels();
    }

    private int IndexOfItem(CollectedItem item)
    {
        var items = GetItems();
        for (int i = 0; i < items.Count; i++)
            if (items[i] == item) return i;
        return -1;
    }

    private IReadOnlyList<CollectedItem> GetItems()
    {
        if (physicalInventory == null) physicalInventory = PhysicalInventory.Instance;
        return physicalInventory?.Items ?? (IReadOnlyList<CollectedItem>)new List<CollectedItem>();
    }

    // ── camera & layer setup ──────────────────────────────────────────────────

    private int ResolveInspectionLayer()
    {
        int named = LayerMask.NameToLayer("Inspection");
        if (named >= 0)
        {
            Debug.Log($"[Inspection] Uso layer 'Inspection' (index {named}).");
            return named;
        }
        for (int i = 8; i <= 31; i++)
        {
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
            {
                Debug.Log($"[Inspection] Uso layer libero index {i} come fallback.");
                return i;
            }
        }
        Debug.LogError("[Inspection] Nessun layer libero trovato!");
        return 0;
    }

    private Camera FindMainCamera()
    {
        var fps = FindFirstObjectByType<FPSController>();
        if (fps?.cameraTransform != null)
        {
            var cam = fps.cameraTransform.GetComponent<Camera>();
            if (cam != null) return cam;
        }
        return Camera.main;
    }

    private void SetupOverlayCamera()
    {
        mainCamera.cullingMask &= ~(1 << inspectionLayer);

        var go = new GameObject("InspectionOverlayCamera");
        go.transform.SetParent(mainCamera.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        overlayCamera                 = go.AddComponent<Camera>();
        overlayCamera.clearFlags      = CameraClearFlags.SolidColor;
        overlayCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        overlayCamera.cullingMask     = 1 << inspectionLayer;
        overlayCamera.depth           = mainCamera.depth + 10f;
        overlayCamera.fieldOfView     = mainCamera.fieldOfView;
        overlayCamera.nearClipPlane   = 0.05f;
        overlayCamera.farClipPlane    = 20f;
        overlayCamera.allowHDR        = false;
        overlayCamera.allowMSAA       = false;
        overlayCamera.enabled         = false;

        EnsureOverlayTexture();
        SetupOverlayLight(go.transform);

        Debug.Log($"[Inspection] overlayCamera creata. layer={inspectionLayer}, depth={overlayCamera.depth}");
    }

    private void NormalizeModelInPivot(Transform pivot, Transform model, float scaleMultiplier)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds    = new Bounds();
        bool   hasBounds = false;

        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

            // Le SkinnedMeshRenderer usano la geometria realmente POSATA (BakeMesh): i
            // bounds salvati all'import (r.bounds / localBounds) negli FBX rigati possono
            // essere sbagliati e far apparire il modello microscopico. Le mesh statiche
            // restano su r.bounds, invariate.
            bool skinned = r is SkinnedMeshRenderer smr0 && smr0.sharedMesh != null;
            Bounds worldBounds = skinned ? SkinnedWorldBounds((SkinnedMeshRenderer)r) : r.bounds;

            if (debugInspectionSizing)
                Debug.Log($"[Inspection]   renderer '{r.name}' type={r.GetType().Name} " +
                          $"r.bounds={r.bounds.size} used={(skinned ? "baked" : "r.bounds")}={worldBounds.size}");

            if (!hasBounds) { bounds = worldBounds; hasBounds = true; }
            else            bounds.Encapsulate(worldBounds);
        }

        if (!hasBounds)
        {
            pivot.localScale = Vector3.one * GetInspectionSize(scaleMultiplier);
            return;
        }

        Vector3 localCenter = pivot.InverseTransformPoint(bounds.center);
        model.localPosition -= localCenter;

        float maxDim     = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float targetSize = GetInspectionSize(scaleMultiplier);
        pivot.localScale = Vector3.one * (maxDim > 0f ? targetSize / maxDim : targetSize);

        if (debugInspectionSizing)
            Debug.Log($"[Inspection] '{model.name}' renderers={renderers.Length} mult={scaleMultiplier} " +
                      $"boundsSize={bounds.size} maxDim={maxDim:F5} targetSize={targetSize:F4} pivotScale={pivot.localScale.x:F5}");
    }

    // Bounds world-space della geometria realmente posata di una skinned mesh. BakeMesh
    // produce i vertici nello spazio locale del renderer (senza scala del transform),
    // quindi li riportiamo in world con localToWorldMatrix.
    private Bounds SkinnedWorldBounds(SkinnedMeshRenderer smr)
    {
        if (bakeScratch == null) bakeScratch = new Mesh { name = "__inspectionBakeScratch" };
        smr.BakeMesh(bakeScratch);
        bakeScratch.RecalculateBounds();

        Bounds local  = bakeScratch.bounds;
        Matrix4x4 m   = smr.transform.localToWorldMatrix;
        Vector3 c     = local.center;
        Vector3 e     = local.extents;

        Bounds world  = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            world.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return world;
    }

    private float GetInspectionSize(float scaleMultiplier) =>
        normalizedSize * Mathf.Max(0.05f, scaleMultiplier);

    private void SetupOverlayLight(Transform parent)
    {
        var lightGo = new GameObject("InspectionOverlayLight");
        lightGo.transform.SetParent(parent, false);
        lightGo.transform.localRotation = Quaternion.Euler(35f, -25f, 0f);

        overlayLight = lightGo.AddComponent<Light>();
        overlayLight.type        = LightType.Directional;
        overlayLight.intensity   = 1.3f;
        overlayLight.cullingMask = 1 << inspectionLayer;
        overlayLight.enabled     = false;
    }

    private void EnsureOverlayTexture()
    {
        int width  = Mathf.Max(Screen.width,  640);
        int height = Mathf.Max(Screen.height, 480);

        if (overlayTexture       != null
            && overlayTextureWidth  == width
            && overlayTextureHeight == height)
            return;

        if (overlayCamera != null) overlayCamera.targetTexture = null;
        ReleaseOverlayTexture();

        overlayTextureWidth  = width;
        overlayTextureHeight = height;
        overlayTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        overlayTexture.name             = "InspectionOverlayRT";
        overlayTexture.filterMode       = FilterMode.Bilinear;
        overlayTexture.useMipMap        = false;
        overlayTexture.autoGenerateMips = false;
        overlayTexture.Create();

        if (overlayCamera    != null) overlayCamera.targetTexture = overlayTexture;
        if (modelRenderImage != null) modelRenderImage.texture     = overlayTexture;
    }

    private void ReleaseOverlayTexture()
    {
        if (overlayTexture == null) return;
        overlayTexture.Release();
        Destroy(overlayTexture);
        overlayTexture = null;
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void FreezePlayer(bool freeze)
    {
        if (playerController != null) playerController.gameplayFrozen = freeze;
        Cursor.lockState = freeze ? CursorLockMode.Confined : CursorLockMode.Locked;
        Cursor.visible   = freeze;
    }

    // ── UI build ──────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvasGo = new GameObject("InspectionOverlayUI");
        DontDestroyOnLoad(canvasGo);
        overlayCanvas              = canvasGo.AddComponent<Canvas>();
        overlayCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 50;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = overlayCanvas.GetComponent<RectTransform>();

        var backdrop = MakeImage("Backdrop", root, new Color(0f, 0f, 0f, 0.72f), stretch: true);
        backdrop.raycastTarget = true;

        modelRenderImage         = MakeRawImage("ModelRender", root, stretch: true);
        modelRenderImage.texture = overlayTexture;

        // ── info bar in basso (pannello scuro con nome, descrizione, hint) ──
        var infoBar = MakeRect("InfoBar", root);
        infoBar.anchorMin = infoBarAnchorMin;
        infoBar.anchorMax = infoBarAnchorMax;
        infoBar.offsetMin = infoBar.offsetMax = Vector2.zero;
        var bg = MakeImage("InfoBarBG", infoBar, infoBarColor, stretch: true);
        if (infoBarSprite != null)
        {
            bg.sprite = infoBarSprite;
            bg.type   = Image.Type.Sliced;
        }

        nameText = MakeTMP("ItemName", infoBar, nameFontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        nameText.rectTransform.anchorMin = nameAnchorMin;
        nameText.rectTransform.anchorMax = nameAnchorMax;
        nameText.rectTransform.offsetMin = nameText.rectTransform.offsetMax = Vector2.zero;

        descText = MakeTMP("ItemDesc", infoBar, descriptionFontSize, TextAlignmentOptions.Center, FontStyles.Normal);
        descText.rectTransform.anchorMin = descAnchorMin;
        descText.rectTransform.anchorMax = descAnchorMax;
        descText.rectTransform.offsetMin = descText.rectTransform.offsetMax = Vector2.zero;
        descText.color = new Color(0.75f, 0.75f, 0.75f);

        hintText = MakeTMP("Hint", infoBar, hintFontSize, TextAlignmentOptions.Center, FontStyles.Normal);
        hintText.rectTransform.anchorMin = hintAnchorMin;
        hintText.rectTransform.anchorMax = hintAnchorMax;
        hintText.rectTransform.offsetMin = hintText.rectTransform.offsetMax = Vector2.zero;
        hintText.color = new Color(0.45f, 0.45f, 0.45f);
        UpdateHintText();

        overlayCanvas.gameObject.SetActive(false);
    }

    // ── helpers UI ────────────────────────────────────────────────────────────

    private RectTransform MakeRect(string n, RectTransform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var r  = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        return r;
    }

    private Image MakeImage(string n, RectTransform parent, Color color, bool stretch = false)
    {
        var go  = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var r   = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        if (stretch)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
        go.GetComponent<Image>().color = color;
        return go.GetComponent<Image>();
    }

    private RawImage MakeRawImage(string n, RectTransform parent, bool stretch = false)
    {
        var go  = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var r   = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        if (stretch)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
        var img = go.GetComponent<RawImage>();
        img.color         = Color.white;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI MakeTMP(string n, RectTransform parent, float size,
        TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var r  = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize         = size;
        t.alignment        = align;
        t.fontStyle        = style;
        t.color            = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget    = false;
        if (uiFont != null) t.font = uiFont;
        return t;
    }
}
