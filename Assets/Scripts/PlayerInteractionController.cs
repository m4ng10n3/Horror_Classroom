using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(FPSController))]
[RequireComponent(typeof(EscapeInventory))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("References")]
    public FPSController player;
    public Camera playerCamera;
    public EscapeInventory inventory;
    public GameManager gameManager;
    public DoorEscape escapeDoor;

    [Header("Interaction")]
    public float interactionDistance = 3f;
    public LayerMask interactionMask = ~0;
    public Key interactKey = Key.F;

    [Header("Canvas HUD")]
    public bool drawPlaceholderHud = true;
    public float messageDuration = 4f;

    private IPlayerInteractable currentInteractable;
    private string currentPrompt = string.Empty;
    private string currentMessage = string.Empty;
    private float messageTimer = 0f;

    private RectTransform hudRoot;
    private Image inventoryPanelImage;
    private Image promptPanelImage;
    private Image dialoguePanelImage;
    private TextMeshProUGUI inventoryText;
    private TextMeshProUGUI promptText;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI crosshairText;

    void Reset()
    {
        AutoAssignReferences();
    }

    void Awake()
    {
        AutoAssignReferences();
        EnsureCanvasHud();
        RefreshHud();
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
        }

        EnsureCanvasHud();

        if (player == null || playerCamera == null)
        {
            RefreshHud();
            return;
        }

        if (player.gameplayFrozen || player.isSeated)
        {
            ClearCurrentInteractable();
            RefreshHud();
            return;
        }

        UpdateCurrentInteractable();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && currentInteractable != null && keyboard[interactKey].wasPressedThisFrame)
        {
            ShowMessage(currentInteractable.Interact(inventory, gameManager));
        }

        RefreshHud();
    }

    private void AutoAssignReferences()
    {
        if (player == null)
        {
            player = GetComponent<FPSController>();
        }

        if (inventory == null)
        {
            inventory = GetComponent<EscapeInventory>();
        }

        if (playerCamera == null)
        {
            if (player != null && player.cameraTransform != null)
            {
                playerCamera = player.cameraTransform.GetComponent<Camera>();
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        if (escapeDoor == null)
        {
            escapeDoor = FindFirstObjectByType<DoorEscape>();
        }
    }

    private void UpdateCurrentInteractable()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, QueryTriggerInteraction.Collide)
            && TryGetInteractable(hit.collider, out IPlayerInteractable interactable)
            && interactable.CanInteract(inventory))
        {
            currentInteractable = interactable;
            currentPrompt = interactable.GetInteractionPrompt(inventory);
            return;
        }

        ClearCurrentInteractable();
    }

    private bool TryGetInteractable(Collider hitCollider, out IPlayerInteractable interactable)
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlayerInteractable candidate)
            {
                interactable = candidate;
                return true;
            }
        }

        interactable = null;
        return false;
    }

    private void ClearCurrentInteractable()
    {
        currentInteractable = null;
        currentPrompt = string.Empty;
    }

    private void ShowMessage(string message)
    {
        currentMessage = message;
        messageTimer = messageDuration;
        RefreshHud();
    }

    private void EnsureCanvasHud()
    {
        if (!drawPlaceholderHud || hudRoot != null)
        {
            return;
        }

        Canvas targetCanvas = ResolveTargetCanvas();
        if (targetCanvas == null)
        {
            return;
        }

        Image questionPanelStyle = null;
        TextMeshProUGUI questionTextStyle = null;
        Image answerButtonStyle = null;
        TextMeshProUGUI answerButtonTextStyle = null;

        if (gameManager != null)
        {
            if (gameManager.questionPanel != null)
            {
                questionPanelStyle = gameManager.questionPanel.GetComponent<Image>();
            }

            questionTextStyle = gameManager.questionText;

            if (gameManager.answerButtons != null && gameManager.answerButtons.Length > 0 && gameManager.answerButtons[0] != null)
            {
                answerButtonStyle = gameManager.answerButtons[0].GetComponent<Image>();
                answerButtonTextStyle = gameManager.answerButtons[0].GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        hudRoot = CreateRectTransform("InteractionHUD", targetCanvas.transform as RectTransform);
        StretchToParent(hudRoot);
        hudRoot.SetAsLastSibling();

        RectTransform inventoryPanel = CreatePanel(
            "InventoryPanel",
            hudRoot,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-28f, -28f),
            new Vector2(420f, 130f),
            new Vector2(1f, 1f));
        inventoryPanelImage = inventoryPanel.GetComponent<Image>();
        ApplyImageStyle(inventoryPanelImage, questionPanelStyle, new Color(0f, 0f, 0f, 0.72f));
        inventoryText = CreateText(
            "InventoryText",
            inventoryPanel,
            questionTextStyle,
            24f,
            TextAlignmentOptions.TopLeft);
        SetTextPadding(inventoryText.rectTransform, 26f, 18f, 26f, 18f);

        RectTransform promptPanel = CreatePanel(
            "InteractionPromptPanel",
            hudRoot,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 224f),
            new Vector2(360f, 56f),
            new Vector2(0.5f, 0f));
        promptPanelImage = promptPanel.GetComponent<Image>();
        ApplyImageStyle(promptPanelImage, answerButtonStyle, new Color(0.15686275f, 0.15686275f, 0.23529412f, 0.95f));
        promptText = CreateText(
            "InteractionPromptText",
            promptPanel,
            answerButtonTextStyle != null ? answerButtonTextStyle : questionTextStyle,
            24f,
            TextAlignmentOptions.Center);
        StretchToParent(promptText.rectTransform);

        RectTransform dialoguePanel = CreatePanel(
            "DialoguePanel",
            hudRoot,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 32f),
            new Vector2(980f, 172f),
            new Vector2(0.5f, 0f));
        dialoguePanelImage = dialoguePanel.GetComponent<Image>();
        ApplyImageStyle(dialoguePanelImage, questionPanelStyle, new Color(0f, 0f, 0f, 0.78f));
        dialogueText = CreateText(
            "DialogueText",
            dialoguePanel,
            questionTextStyle,
            27f,
            TextAlignmentOptions.TopLeft);
        SetTextPadding(dialogueText.rectTransform, 32f, 22f, 32f, 22f);

        RectTransform crosshair = CreateRectTransform("InteractionCrosshair", hudRoot);
        crosshair.anchorMin = new Vector2(0.5f, 0.5f);
        crosshair.anchorMax = new Vector2(0.5f, 0.5f);
        crosshair.pivot = new Vector2(0.5f, 0.5f);
        crosshair.anchoredPosition = Vector2.zero;
        crosshair.sizeDelta = new Vector2(24f, 24f);
        crosshairText = CreateText(
            "CrosshairText",
            crosshair,
            answerButtonTextStyle != null ? answerButtonTextStyle : questionTextStyle,
            30f,
            TextAlignmentOptions.Center);
        StretchToParent(crosshairText.rectTransform);
        crosshairText.text = "+";
    }

    private Canvas ResolveTargetCanvas()
    {
        if (gameManager != null && gameManager.questionPanel != null)
        {
            Canvas questionCanvas = gameManager.questionPanel.GetComponentInParent<Canvas>();
            if (questionCanvas != null)
            {
                return questionCanvas;
            }
        }

        return FindFirstObjectByType<Canvas>();
    }

    private RectTransform CreatePanel(
        string objectName,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot)
    {
        RectTransform rect = CreateRectTransform(objectName, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.pivot = pivot;
        rect.gameObject.AddComponent<CanvasRenderer>();
        rect.gameObject.AddComponent<Image>();
        return rect;
    }

    private RectTransform CreateRectTransform(string objectName, RectTransform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        RectTransform parent,
        TextMeshProUGUI styleSource,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        ApplyTextStyle(text, styleSource, fontSize, alignment);
        return text;
    }

    private void ApplyImageStyle(Image target, Image source, Color fallbackColor)
    {
        if (target == null)
        {
            return;
        }

        if (source != null)
        {
            target.sprite = source.sprite;
            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
        }
        else
        {
            target.type = Image.Type.Sliced;
        }

        target.color = fallbackColor;
        target.raycastTarget = false;
    }

    private void ApplyTextStyle(
        TextMeshProUGUI target,
        TextMeshProUGUI source,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        if (target == null)
        {
            return;
        }

        if (source != null)
        {
            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.color = source.color;
            target.fontStyle = source.fontStyle;
            target.lineSpacing = source.lineSpacing;
            target.characterSpacing = source.characterSpacing;
            target.wordSpacing = source.wordSpacing;
            target.textWrappingMode = source.textWrappingMode;
            target.richText = source.richText;
        }
        else
        {
            target.color = Color.white;
            target.textWrappingMode = TextWrappingModes.Normal;
            target.richText = true;
        }

        target.fontSize = fontSize;
        target.alignment = alignment;
        target.overflowMode = TextOverflowModes.Overflow;
        target.raycastTarget = false;
    }

    private void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void SetTextPadding(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void RefreshHud()
    {
        if (!drawPlaceholderHud || hudRoot == null)
        {
            return;
        }

        bool showGameplayHud = ShouldShowGameplayHud();
        bool showCrosshair = showGameplayHud && player != null && !player.isSeated;
        bool showPrompt = showCrosshair && !string.IsNullOrWhiteSpace(currentPrompt);
        bool showDialogue = showGameplayHud && messageTimer > 0f && !string.IsNullOrWhiteSpace(currentMessage);

        if (inventoryPanelImage != null)
        {
            inventoryPanelImage.gameObject.SetActive(showGameplayHud);
        }

        if (promptPanelImage != null)
        {
            promptPanelImage.gameObject.SetActive(showPrompt);
        }

        if (dialoguePanelImage != null)
        {
            dialoguePanelImage.gameObject.SetActive(showDialogue);
        }

        if (crosshairText != null)
        {
            crosshairText.gameObject.SetActive(showCrosshair);
        }

        if (inventoryText != null)
        {
            inventoryText.text = BuildInventoryText();
        }

        if (promptText != null)
        {
            promptText.text = currentPrompt;
        }

        if (dialogueText != null)
        {
            dialogueText.text = $"<b>Interazione</b>\n{currentMessage}";
        }
    }

    private bool ShouldShowGameplayHud()
    {
        if (player != null && player.gameplayFrozen)
        {
            return false;
        }

        if (gameManager != null && gameManager.questionPanel != null && gameManager.questionPanel.activeInHierarchy)
        {
            return false;
        }

        return true;
    }

    private string BuildInventoryText()
    {
        if (inventory == null)
        {
            return "<b>Inventario</b>\nNon assegnato.";
        }

        int targetCrafts = escapeDoor != null ? escapeDoor.requiredCraftedItems : 3;
        return $"<b>Inventario</b>\nOggetti base: {inventory.GetRawItemsSummary()}\nOggetti fuga ({inventory.CraftedItemCount}/{targetCrafts}): {inventory.GetCraftedItemsSummary()}";
    }
}
