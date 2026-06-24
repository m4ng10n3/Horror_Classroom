using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(FPSController))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("References")]
    public FPSController player;
    public Camera playerCamera;
    public GameManager gameManager;
    public DoorEscape escapeDoor;

    [Header("Interaction")]
    public float interactionDistance = 3f;
    [Min(0f)]
    public float interactionRadius = 0.25f;
    [Min(1)]
    public int maxInteractionHits = 24;
    public LayerMask interactionMask = ~0;
    public Key interactKey = Key.F;
    [Tooltip("Tasto per l'azione secondaria opzionale (es. chiudere un cassetto senza raccogliere). Deve combaciare col testo del prompt secondario.")]
    public Key secondaryInteractKey = Key.Q;
    [Tooltip("Distanza oltre la quale un dialogo in corso viene chiuso se il player si allontana dall'interlocutore. Se <= 0 usa interactionDistance.")]
    public float dialogueMaxDistance = 0f;

    [Header("Canvas HUD")]
    public bool drawPlaceholderHud = true;
    public float messageDuration = 4f;

    [Header("Dialogue")]
    public string playerName = "Eddy";
    public Color playerLineColor = new Color(1f, 0.91f, 0.49f);
    public Color npcLineColor = Color.white;

    private IPlayerInteractable currentInteractable;
    private IHoldInteractable activeHold;
    private float holdTimer;
    private string currentPrompt = string.Empty;
    private string currentMessage = string.Empty;
    private float messageTimer = 0f;

    private bool isInDialogue = false;
    private DialogueLine[] activeSequence;
    private int currentLineIndex;
    private string currentNPCName = string.Empty;
    private IDialogueSequenceInteractable currentDialogueSource;
    private Transform currentSpeakerTransform;
    private Collider currentSpeakerCollider;
    private RaycastHit[] interactionHits;

    private RectTransform hudRoot;
    private Image promptPanelImage;
    private Image dialoguePanelImage;
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
        EnsureInteractionHitBuffer();
        EnsureCanvasHud();
        RefreshHud();
    }

    void Update()
    {
        if (messageTimer > 0f && !isInDialogue)
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
            CancelHold();
            if (!isInDialogue)
                ClearCurrentInteractable();
            RefreshHud();
            return;
        }

        if (!isInDialogue)
            UpdateCurrentInteractable();
        else if (HasWalkedAwayFromSpeaker())
            EndDialogue();

        Keyboard keyboard = Keyboard.current;

        // Interazioni "tieni premuto" (es. furto) gestite a parte: se una è in corso o
        // appena avviata, consuma il frame e salta il tap istantaneo.
        bool holdConsumed = !isInDialogue && HandleHoldInteraction(keyboard);

        if (!holdConsumed && keyboard != null && keyboard[interactKey].wasPressedThisFrame)
        {
            if (isInDialogue)
            {
                AdvanceDialogue();
            }
            else if (currentInteractable != null && !(currentInteractable is IHoldInteractable))
            {
                if (currentInteractable is IDialogueSequenceInteractable seqInteractable)
                    StartDialogueSequence(seqInteractable);
                else
                    ShowMessage(currentInteractable.Interact(gameManager));
            }
        }

        if (keyboard != null && !isInDialogue
            && currentInteractable is ISecondaryInteractable secondaryInteractable
            && secondaryInteractable.CanInteractSecondary()
            && keyboard[secondaryInteractKey].wasPressedThisFrame)
        {
            ShowMessage(secondaryInteractable.InteractSecondary(gameManager));
        }

        RefreshHud();
    }

    private void StartDialogueSequence(IDialogueSequenceInteractable seqInteractable)
    {
        activeSequence = seqInteractable.GetDialogueSequence(gameManager);
        currentDialogueSource = seqInteractable;
        currentNPCName = seqInteractable.SpeakerName;
        MonoBehaviour speakerBehaviour = seqInteractable as MonoBehaviour;
        currentSpeakerTransform = speakerBehaviour != null ? speakerBehaviour.transform : null;
        currentSpeakerCollider = speakerBehaviour != null ? speakerBehaviour.GetComponentInChildren<Collider>() : null;
        currentLineIndex = 0;
        isInDialogue = activeSequence != null && activeSequence.Length > 0;
        ShowCurrentDialogueLine();
    }

    private void AdvanceDialogue()
    {
        currentLineIndex++;
        ShowCurrentDialogueLine();
    }

    // Gestisce le interazioni a pressione prolungata (IHoldInteractable). Ritorna true se
    // un hold è stato avviato o è in corso questo frame, così il chiamante salta il tap.
    private bool HandleHoldInteraction(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            CancelHold();
            return false;
        }

        bool keyHeld = keyboard[interactKey].isPressed;

        // Avvio di un nuovo hold: serve una nuova pressione mentre si punta un IHoldInteractable.
        if (activeHold == null)
        {
            if (!keyboard[interactKey].wasPressedThisFrame)
                return false;
            if (!(currentInteractable is IHoldInteractable holdable))
                return false;

            if (!holdable.CanBeginHold(out string blockReason))
            {
                if (!string.IsNullOrWhiteSpace(blockReason))
                    ShowMessage(blockReason);
                return true; // pressione consumata dal tentativo bloccato
            }

            activeHold = holdable;
            holdTimer = 0f;
            // continua sotto col primo tick
        }

        // Interruzione: tasto rilasciato, bersaglio non più puntato/in range, o non più interagibile.
        if (!keyHeld || (object)currentInteractable != (object)activeHold || !activeHold.CanInteract())
        {
            CancelHold();
            return false;
        }

        holdTimer += Time.deltaTime;
        float progress = activeHold.HoldDuration > 0f
            ? Mathf.Clamp01(holdTimer / activeHold.HoldDuration)
            : 1f;

        // Tick: l'oggetto può richiedere l'annullamento (es. esposizione al massimo).
        if (!activeHold.OnHoldTick(Time.deltaTime, progress))
        {
            CancelHold();
            return true;
        }

        // Feedback di avanzamento nel prompt (la barra di esposizione fa il resto).
        currentPrompt = $"{currentInteractable.GetInteractionPrompt()}  {Mathf.RoundToInt(progress * 100f)}%";

        if (holdTimer >= activeHold.HoldDuration)
        {
            IHoldInteractable finished = activeHold;
            activeHold = null;
            finished.OnHoldCompleted(gameManager);
        }

        return true;
    }

    private void CancelHold()
    {
        if (activeHold == null)
            return;

        IHoldInteractable cancelled = activeHold;
        activeHold = null;
        cancelled.OnHoldCancelled();
    }

    private bool HasWalkedAwayFromSpeaker()
    {
        if (currentSpeakerTransform == null)
            return false;

        float maxDistance = dialogueMaxDistance > 0f ? dialogueMaxDistance : interactionDistance;
        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;

        // Misura dal punto piu' vicino del collider dell'NPC: il pivot e' ai piedi,
        // mentre la camera del player e' all'altezza degli occhi, quindi la distanza
        // dal pivot sarebbe falsata e chiuderebbe il dialogo subito.
        Vector3 target = currentSpeakerCollider != null
            ? currentSpeakerCollider.ClosestPoint(origin)
            : currentSpeakerTransform.position;

        return Vector3.Distance(origin, target) > maxDistance;
    }

    // Chiusura per completamento: applica gli effetti collaterali del dialogo.
    private void CompleteDialogue()
    {
        currentDialogueSource?.OnDialogueCompleted(gameManager);
        EndDialogue();
    }

    // Chiusura senza commit: usata quando il dialogo viene interrotto (player
    // allontanato). Lo stato dell'NPC resta invariato, così riparte dall'inizio.
    private void EndDialogue()
    {
        isInDialogue = false;
        activeSequence = null;
        currentDialogueSource = null;
        currentSpeakerTransform = null;
        currentSpeakerCollider = null;
        currentMessage = string.Empty;
        messageTimer = 0f;
        RefreshHud();
    }

    private void ShowCurrentDialogueLine()
    {
        if (!isInDialogue || activeSequence == null || currentLineIndex >= activeSequence.Length)
        {
            CompleteDialogue();
            return;
        }

        DialogueLine line = activeSequence[currentLineIndex];
        bool isPlayer = line.speaker == DialogueLine.Speaker.Player;
        string name = isPlayer ? playerName : currentNPCName;
        Color col = isPlayer ? playerLineColor : npcLineColor;
        string hex = ColorUtility.ToHtmlStringRGB(col);
        currentMessage = $"<color=#{hex}><b>{name}</b>: \"{line.text}\"</color>";
        messageTimer = float.MaxValue;
        RefreshHud();
    }

    private void AutoAssignReferences()
    {
        if (player == null)
        {
            player = GetComponent<FPSController>();
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
        if (TryFindInteractable(ray, out IPlayerInteractable interactable))
        {
            currentInteractable = interactable;
            currentPrompt = interactable.GetInteractionPrompt();
            if (interactable is ISecondaryInteractable secondaryInteractable && secondaryInteractable.CanInteractSecondary())
            {
                string secondaryPrompt = secondaryInteractable.GetSecondaryPrompt();
                if (!string.IsNullOrWhiteSpace(secondaryPrompt))
                    currentPrompt += "\n" + secondaryPrompt;
            }
            return;
        }

        ClearCurrentInteractable();
    }

    private bool TryFindInteractable(Ray ray, out IPlayerInteractable interactable)
    {
        EnsureInteractionHitBuffer();

        int hitCount = interactionRadius > 0f
            ? Physics.SphereCastNonAlloc(ray, interactionRadius, interactionHits, interactionDistance, interactionMask, QueryTriggerInteraction.Collide)
            : Physics.RaycastNonAlloc(ray, interactionHits, interactionDistance, interactionMask, QueryTriggerInteraction.Collide);

        if (hitCount <= 0)
        {
            interactable = null;
            return false;
        }

        Array.Sort(interactionHits, 0, hitCount, RaycastHitDistanceComparer.Instance);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = interactionHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (TryGetInteractable(hitCollider, out IPlayerInteractable candidate) && candidate.CanInteract())
            {
                interactable = candidate;
                return true;
            }
        }

        interactable = null;
        return false;
    }

    private bool TryGetInteractable(Collider hitCollider, out IPlayerInteractable interactable)
    {
        if (TryGetInteractableFromBehaviours(hitCollider.GetComponentsInParent<MonoBehaviour>(true), out interactable))
        {
            return true;
        }

        if (TryGetInteractableFromBehaviours(hitCollider.GetComponentsInChildren<MonoBehaviour>(true), out interactable))
        {
            return true;
        }

        Rigidbody attachedRigidbody = hitCollider.attachedRigidbody;
        if (attachedRigidbody != null)
        {
            Transform root = attachedRigidbody.transform;
            if (TryGetInteractableFromBehaviours(root.GetComponentsInParent<MonoBehaviour>(true), out interactable))
            {
                return true;
            }

            if (TryGetInteractableFromBehaviours(root.GetComponentsInChildren<MonoBehaviour>(true), out interactable))
            {
                return true;
            }
        }

        interactable = null;
        return false;
    }

    private bool TryGetInteractableFromBehaviours(IEnumerable<MonoBehaviour> behaviours, out IPlayerInteractable interactable)
    {
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IPlayerInteractable candidate)
            {
                interactable = candidate;
                return true;
            }
        }

        interactable = null;
        return false;
    }

    private void EnsureInteractionHitBuffer()
    {
        int capacity = Mathf.Max(1, maxInteractionHits);
        if (interactionHits == null || interactionHits.Length != capacity)
        {
            interactionHits = new RaycastHit[capacity];
        }
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
        bool showDialogue = showGameplayHud && !string.IsNullOrWhiteSpace(currentMessage) &&
                            (isInDialogue || messageTimer > 0f);

        string displayPrompt;
        bool showPrompt;
        if (isInDialogue)
        {
            bool isLastLine = activeSequence == null || currentLineIndex >= activeSequence.Length - 1;
            displayPrompt = isLastLine ? $"[F] Chiudi" : $"[F] Continua";
            showPrompt = showGameplayHud;
        }
        else
        {
            displayPrompt = currentPrompt;
            showPrompt = showCrosshair && !string.IsNullOrWhiteSpace(currentPrompt);
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
            crosshairText.gameObject.SetActive(showCrosshair && !isInDialogue);
        }

        if (promptText != null)
        {
            promptText.text = displayPrompt;
        }

        if (dialogueText != null)
        {
            dialogueText.text = isInDialogue ? currentMessage : $"<b>Interazione</b>\n{currentMessage}";
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

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }
    }

}
