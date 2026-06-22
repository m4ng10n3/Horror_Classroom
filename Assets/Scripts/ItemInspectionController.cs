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
        if (isInspecting || GetItems().Count == 0) return;

        currentIndex = Mathf.Clamp(currentIndex, 0, GetItems().Count - 1);
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
        if (items.Count == 0) { CloseInspection(); return; }
        currentIndex = Mathf.Clamp(currentIndex, 0, items.Count - 1);
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

        return droppedObject;
    }

    private Vector3 GetDropPosition()
    {
        Transform source = mainCamera != null ? mainCamera.transform : transform;
        Vector3 position = source.position + source.forward * 1.1f;

        if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.08f;
        }

        return position;
    }

    private void UpdateInfoBar()
    {
        var items = GetItems();
        if (items.Count == 0 || currentIndex >= items.Count)
        {
            if (nameText != null) nameText.text = "";
            if (descText != null) descText.text  = "";
            return;
        }
        var cur = items[currentIndex];
        if (nameText != null) nameText.text = cur?.name ?? "";
        if (descText != null) descText.text  = cur?.description ?? "";
    }

    private void UpdateHintText()
    {
        if (hintText == null) return;
        string actionHint = currentAction != null ? $"   {currentAction.HintText}" : "";
        hintText.text = $"{navigateHintText}   {rotateHintText}{actionHint}   {dropHintText}   {closeHintText}";
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
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else              bounds.Encapsulate(r.bounds);
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
