using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ItemInspectionController : MonoBehaviour
{
    public static ItemInspectionController Instance { get; private set; }

    [Header("Impostazioni")]
    public float inspectionDistance = 1.5f;
    public float normalizedSize     = 0.8f;
    public float rotationSpeed      = 120f;

    public bool IsInspecting => isInspecting;

    private bool          isInspecting;
    private GameObject    currentModel;
    private Camera        mainCamera;
    private Camera        overlayCamera;
    private FPSController playerController;
    private int           inspectionLayer = -1;

    private Canvas          overlayCanvas;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI descText;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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
    }

    void Update()
    {
        if (!isInspecting) return;

        var kb = Keyboard.current;
        if (kb != null && (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
        {
            CloseInspection();
            return;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed && currentModel != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            currentModel.transform.Rotate(mainCamera.transform.up,
                -delta.x * rotationSpeed * Time.deltaTime, Space.World);
            currentModel.transform.Rotate(mainCamera.transform.right,
                delta.y * rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    public void OpenInspection(CollectedItem item)
    {
        if (item?.worldSource == null)
        {
            Debug.LogWarning("[Inspection] worldSource è null.");
            return;
        }
        if (overlayCamera == null)
        {
            Debug.LogError("[Inspection] overlayCamera null. Controlla la Console per errori precedenti.");
            return;
        }

        if (isInspecting) CloseInspection();

        currentModel = Instantiate(item.worldSource);
        currentModel.SetActive(true);
        currentModel.transform.SetParent(overlayCamera.transform, false);
        currentModel.transform.localPosition = new Vector3(0f, 0f, inspectionDistance);
        currentModel.transform.localRotation = Quaternion.Euler(15f, -25f, 0f);
        currentModel.transform.localScale    = Vector3.one;

        NormalizeScale(currentModel);
        SetLayerRecursive(currentModel, inspectionLayer);

        if (nameText != null) nameText.text = item.name;
        if (descText  != null) descText.text = item.description;

        overlayCamera.enabled = true;
        overlayCanvas.gameObject.SetActive(true);
        FreezePlayer(true);
        isInspecting = true;
    }

    public void CloseInspection()
    {
        isInspecting = false;
        if (currentModel   != null) { Destroy(currentModel); currentModel = null; }
        if (overlayCamera  != null) overlayCamera.enabled = false;
        if (overlayCanvas  != null) overlayCanvas.gameObject.SetActive(false);
        FreezePlayer(false);
    }

    // ─────────────────────────────────────────────────────────────
    //  Trova automaticamente un layer libero — nessun setup manuale
    // ─────────────────────────────────────────────────────────────

    private int ResolveInspectionLayer()
    {
        // Controlla se esiste il layer "Inspection" impostato dall'utente
        int named = LayerMask.NameToLayer("Inspection");
        if (named >= 0)
        {
            Debug.Log($"[Inspection] Uso layer 'Inspection' (index {named}).");
            return named;
        }

        // Altrimenti trova il primo layer utente senza nome (8–31)
        for (int i = 8; i <= 31; i++)
        {
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
            {
                Debug.Log($"[Inspection] Layer 'Inspection' non trovato in Project Settings. " +
                           $"Uso layer libero index {i} come fallback automatico.");
                return i;
            }
        }

        Debug.LogError("[Inspection] Nessun layer libero trovato (0-31 tutti occupati)!");
        return 0;
    }

    // ─────────────────────────────────────────────────────────────

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
        // Rimuovi il layer di ispezione dalla main camera
        mainCamera.cullingMask &= ~(1 << inspectionLayer);

        var go = new GameObject("InspectionOverlayCamera");
        go.transform.SetParent(mainCamera.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        overlayCamera                 = go.AddComponent<Camera>();
        // SolidColor: la camera ridisegna tutto lo schermo con sfondo scuro,
        // poi renderizza l'oggetto sopra. Il Canvas UI viene dopo ma non ha sfondo.
        overlayCamera.clearFlags      = CameraClearFlags.SolidColor;
        overlayCamera.backgroundColor = new Color(0.06f, 0.06f, 0.09f, 1f);
        overlayCamera.cullingMask     = 1 << inspectionLayer;
        overlayCamera.depth           = mainCamera.depth + 1;
        overlayCamera.fieldOfView     = mainCamera.fieldOfView;
        overlayCamera.nearClipPlane   = 0.05f;
        overlayCamera.farClipPlane    = 20f;
        overlayCamera.enabled         = false;

        Debug.Log($"[Inspection] overlayCamera creata. layer={inspectionLayer}, " +
                  $"cullingMask={overlayCamera.cullingMask}, depth={overlayCamera.depth}");
    }

    private void NormalizeScale(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            go.transform.localScale = Vector3.one * normalizedSize;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        go.transform.localScale = Vector3.one * (maxDim > 0f ? normalizedSize / maxDim : normalizedSize);
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

    // ─────── UI ───────

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

        // Nessuno sfondo UI — ci pensa la camera (ClearFlags.SolidColor).
        // Il canvas contiene solo la barra testo in basso.

        var infoBar = MakeRect("InfoBar", root);
        infoBar.anchorMin = new Vector2(0f, 0f);
        infoBar.anchorMax = new Vector2(1f, 0.18f);
        infoBar.offsetMin = infoBar.offsetMax = Vector2.zero;
        MakeImage("InfoBarBG", infoBar, new Color(0f, 0f, 0f, 0.88f), stretch: true);

        nameText = MakeTMP("ItemName", infoBar, 32f, TextAlignmentOptions.Center, FontStyles.Bold);
        nameText.rectTransform.anchorMin = new Vector2(0.05f, 0.52f);
        nameText.rectTransform.anchorMax = new Vector2(0.95f, 1f);
        nameText.rectTransform.offsetMin = nameText.rectTransform.offsetMax = Vector2.zero;

        descText = MakeTMP("ItemDesc", infoBar, 20f, TextAlignmentOptions.Center, FontStyles.Normal);
        descText.rectTransform.anchorMin = new Vector2(0.05f, 0.22f);
        descText.rectTransform.anchorMax = new Vector2(0.95f, 0.52f);
        descText.rectTransform.offsetMin = descText.rectTransform.offsetMax = Vector2.zero;
        descText.color = new Color(0.75f, 0.75f, 0.75f);

        var hint = MakeTMP("Hint", infoBar, 16f, TextAlignmentOptions.Center, FontStyles.Normal);
        hint.rectTransform.anchorMin = new Vector2(0.05f, 0f);
        hint.rectTransform.anchorMax = new Vector2(0.95f, 0.22f);
        hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
        hint.text  = "[Tasto Sinistro] Ruota   [E] Chiudi";
        hint.color = new Color(0.45f, 0.45f, 0.45f);

        overlayCanvas.gameObject.SetActive(false);
    }

    private RectTransform MakeRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r  = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        return r;
    }

    private Image MakeImage(string name, RectTransform parent, Color color, bool stretch = false)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var r   = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        if (stretch)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private TextMeshProUGUI MakeTMP(string name, RectTransform parent, float size,
        TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
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
        return t;
    }
}
