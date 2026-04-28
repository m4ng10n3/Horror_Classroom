using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(FPSController))]
[RequireComponent(typeof(EscapeInventory))]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("References")]
    public FPSController player;
    public Camera playerCamera;
    public EscapeInventory inventory;
    public GameManager gameManager;

    [Header("Interaction")]
    public float interactionDistance = 3f;
    public LayerMask interactionMask = ~0;
    public Key interactKey = Key.F;

    [Header("Placeholder HUD")]
    public bool drawPlaceholderHud = true;
    public float messageDuration = 4f;

    private IPlayerInteractable currentInteractable;
    private string currentPrompt = string.Empty;
    private string currentMessage = string.Empty;
    private float messageTimer = 0f;

    void Reset()
    {
        AutoAssignReferences();
    }

    void Awake()
    {
        AutoAssignReferences();
    }

    void Update()
    {
        if (player == null || playerCamera == null)
        {
            return;
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
        }

        if (player.gameplayFrozen || player.isSeated)
        {
            ClearCurrentInteractable();
            return;
        }

        UpdateCurrentInteractable();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || currentInteractable == null)
        {
            return;
        }

        if (keyboard[interactKey].wasPressedThisFrame)
        {
            ShowMessage(currentInteractable.Interact(inventory, gameManager));
        }
    }

    void OnGUI()
    {
        if (!drawPlaceholderHud)
        {
            return;
        }

        GUI.Box(new Rect(20f, 20f, 360f, 70f), BuildInventoryText());

        if (!string.IsNullOrWhiteSpace(currentPrompt))
        {
            float width = 300f;
            float height = 35f;
            Rect promptRect = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - 100f,
                width,
                height);
            GUI.Box(promptRect, currentPrompt);
        }

        if (messageTimer > 0f && !string.IsNullOrWhiteSpace(currentMessage))
        {
            float width = 520f;
            float height = 95f;
            Rect messageRect = new Rect(
                20f,
                Screen.height - 130f,
                width,
                height);
            GUI.Box(messageRect, currentMessage);
        }

        Rect crosshairRect = new Rect(
            (Screen.width * 0.5f) - 6f,
            (Screen.height * 0.5f) - 10f,
            12f,
            20f);
        GUI.Label(crosshairRect, "+");
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

    private string BuildInventoryText()
    {
        if (inventory == null)
        {
            return "Inventario non assegnato.";
        }

        return $"Oggetti: {inventory.GetRawItemsSummary()}\nCraftati ({inventory.CraftedItemCount}/3): {inventory.GetCraftedItemsSummary()}";
    }

    private void ShowMessage(string message)
    {
        currentMessage = message;
        messageTimer = messageDuration;
    }
}
