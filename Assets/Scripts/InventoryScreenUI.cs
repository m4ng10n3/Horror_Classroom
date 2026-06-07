using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Schermata inventario aperta con Tab.
/// Mostra tutti gli oggetti raccolti; cliccando su uno con canInspect=true apre la vista 3D.
/// Tutta la UI è generata a runtime, nessun prefab richiesto.
/// </summary>
public class InventoryScreenUI : MonoBehaviour
{
    [Header("Tasto apertura")]
    public Key toggleKey = Key.Tab;

    [Header("Canvas")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;

    [Header("Layout pannello")]
    public Vector2 panelAnchorMin = new Vector2(0.12f, 0.08f);
    public Vector2 panelAnchorMax = new Vector2(0.88f, 0.92f);
    public Vector2 titleAnchorMin = new Vector2(0f, 0.88f);
    public Vector2 titleAnchorMax = Vector2.one;
    public Vector2 scrollAnchorMin = new Vector2(0.02f, 0.09f);
    public Vector2 scrollAnchorMax = new Vector2(0.98f, 0.87f);
    public Vector2 bottomHintAnchorMin = new Vector2(0f, 0f);
    public Vector2 bottomHintAnchorMax = new Vector2(1f, 0.08f);

    [Header("Righe oggetti")]
    [Min(1f)] public float itemRowHeight = 74f;
    [Min(1f)] public float itemRowSpacing = 80f;
    public Vector2 itemTextOffsetMin = new Vector2(20f, 0f);
    public Vector2 itemTextOffsetMax = new Vector2(-20f, 0f);

    [Header("Testi")]
    [Min(1f)] public float titleFontSize = 38f;
    [Min(1f)] public float bottomHintFontSize = 18f;
    [Min(1f)] public float itemFontSize = 24f;
    [Min(1)] public int itemInspectionHintFontSize = 17;
    public string inventoryTitle = "Inventario";
    public string closeHint = "[Tab] Chiudi";
    public string itemInspectionHint = "[Clicca per ispezionare]";

    private bool isOpen;
    private FPSController playerController;
    private PhysicalInventory physicalInventory;

    private Canvas inventoryCanvas;
    private RectTransform itemListContent;

    void Start()
    {
        playerController = FindFirstObjectByType<FPSController>();
        BuildUI();

        physicalInventory = PhysicalInventory.Instance;
        if (physicalInventory != null)
        {
            physicalInventory.OnItemAdded += HandleInventoryChanged;
            physicalInventory.OnItemRemoved += HandleInventoryChanged;
        }
    }

    void OnDestroy()
    {
        if (physicalInventory == null) return;

        physicalInventory.OnItemAdded -= HandleInventoryChanged;
        physicalInventory.OnItemRemoved -= HandleInventoryChanged;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[toggleKey].wasPressedThisFrame)
        {
            if (isOpen) Close();
            else Open();
        }
    }

    private void Open()
    {
        // Non aprire se l'ispezione è già attiva
        if (ItemInspectionController.Instance != null && ItemInspectionController.Instance.IsInspecting)
            return;

        isOpen = true;
        inventoryCanvas.gameObject.SetActive(true);
        if (playerController != null) playerController.gameplayFrozen = true;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        RefreshList();
    }

    private void Close()
    {
        isOpen = false;
        inventoryCanvas.gameObject.SetActive(false);
        if (playerController != null) playerController.gameplayFrozen = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void RefreshList()
    {
        foreach (Transform child in itemListContent)
            Destroy(child.gameObject);

        if (physicalInventory == null)
            physicalInventory = PhysicalInventory.Instance;

        if (physicalInventory == null) return;

        var items = physicalInventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            CreateItemButton(item, i);
        }

        // Aggiorna l'altezza del contenitore scroll
        itemListContent.sizeDelta = new Vector2(0f, items.Count * Mathf.Max(1f, itemRowSpacing));
    }

    private void CreateItemButton(CollectedItem item, int index)
    {
        bool canInspect = item.canInspect && item.worldSource != null;

        var btnGo = new GameObject("Item_" + item.name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.SetParent(itemListContent, false);
        btnRect.localScale = Vector3.one;
        btnRect.anchorMin = new Vector2(0f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(0.5f, 1f);
        btnRect.sizeDelta = new Vector2(0f, Mathf.Max(1f, itemRowHeight));
        btnRect.anchoredPosition = new Vector2(0f, -index * Mathf.Max(1f, itemRowSpacing));

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.18f, 1f);

        var btn = btnGo.GetComponent<Button>();
        var cols = btn.colors;
        cols.normalColor    = new Color(0.12f, 0.12f, 0.18f, 1f);
        cols.highlightedColor = new Color(0.22f, 0.22f, 0.34f, 1f);
        cols.pressedColor   = new Color(0.08f, 0.08f, 0.12f, 1f);
        btn.colors = cols;

        var capturedItem = item;
        btn.onClick.AddListener(() =>
        {
            if (!canInspect) return;
            Close();
            ItemInspectionController.Instance?.OpenInspection(capturedItem);
        });

        // Label
        var labelGo = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.SetParent(btnRect, false);
        labelRect.localScale = Vector3.one;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = itemTextOffsetMin;
        labelRect.offsetMax = itemTextOffsetMax;

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.fontSize = itemFontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.text = canInspect
            ? $"{item.name}  <size={itemInspectionHintFontSize}><color=#888888>{itemInspectionHint}</color></size>"
            : item.name;
    }

    private void HandleInventoryChanged(CollectedItem item)
    {
        if (isOpen)
            RefreshList();
    }

    // ───────── Costruzione UI ─────────

    private void BuildUI()
    {
        var canvasGo = new GameObject("InventoryScreenCanvas");
        DontDestroyOnLoad(canvasGo);
        inventoryCanvas = canvasGo.AddComponent<Canvas>();
        inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        inventoryCanvas.sortingOrder = 40;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = inventoryCanvas.GetComponent<RectTransform>();

        // Overlay scuro
        MakeImage("Overlay", root, new Color(0f, 0f, 0f, 0.80f), true, stretch: true);

        // Pannello centrale
        var panel = MakeRect("Panel", root);
        panel.anchorMin = panelAnchorMin;
        panel.anchorMax = panelAnchorMax;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        MakeImage("PanelBG", panel, new Color(0.07f, 0.07f, 0.10f, 1f), false, stretch: true);

        // Titolo
        var title = MakeTMP("Title", panel, titleFontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        title.rectTransform.anchorMin = titleAnchorMin;
        title.rectTransform.anchorMax = titleAnchorMax;
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
        title.text = inventoryTitle;

        // Hint
        var hint = MakeTMP("Hint", panel, bottomHintFontSize, TextAlignmentOptions.Center, FontStyles.Normal);
        hint.rectTransform.anchorMin = bottomHintAnchorMin;
        hint.rectTransform.anchorMax = bottomHintAnchorMax;
        hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
        hint.text = closeHint;
        hint.color = new Color(0.45f, 0.45f, 0.45f);

        // ScrollView
        var scrollGo = new GameObject("ScrollView",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        var scrollRect2 = scrollGo.GetComponent<RectTransform>();
        scrollRect2.SetParent(panel, false);
        scrollRect2.localScale = Vector3.one;
        scrollRect2.anchorMin = scrollAnchorMin;
        scrollRect2.anchorMax = scrollAnchorMax;
        scrollRect2.offsetMin = scrollRect2.offsetMax = Vector2.zero;
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var vpGo = new GameObject("Viewport",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        var vpRect = vpGo.GetComponent<RectTransform>();
        vpRect.SetParent(scrollRect2, false);
        vpRect.localScale = Vector3.one;
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = vpRect.offsetMax = Vector2.zero;
        vpGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        vpGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        itemListContent = contentGo.GetComponent<RectTransform>();
        itemListContent.SetParent(vpRect, false);
        itemListContent.localScale = Vector3.one;
        itemListContent.anchorMin = new Vector2(0f, 1f);
        itemListContent.anchorMax = Vector2.one;
        itemListContent.pivot = new Vector2(0.5f, 1f);
        itemListContent.offsetMin = itemListContent.offsetMax = Vector2.zero;
        itemListContent.sizeDelta = Vector2.zero;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = itemListContent;
        scroll.viewport = vpRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        inventoryCanvas.gameObject.SetActive(false);
    }

    private RectTransform MakeRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        return r;
    }

    private Image MakeImage(string name, RectTransform parent, Color color, bool raycast, bool stretch = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        if (stretch)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private TextMeshProUGUI MakeTMP(string name, RectTransform parent, float size,
        TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.localScale = Vector3.one;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = style;
        t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }
}
