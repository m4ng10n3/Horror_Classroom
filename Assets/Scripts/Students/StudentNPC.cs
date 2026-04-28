using UnityEngine;

public class StudentNPC : MonoBehaviour, IPlayerInteractable
{
    [Header("Identity")]
    public string studentName = "Studente";
    public Color studentColor = Color.white;

    [Header("Dialogue")]
    [TextArea(2, 4)]
    public string openingDialogue = "Psst...";

    [TextArea(2, 4)]
    public string missingItemDialogue = "Se mi porti l'oggetto giusto, ti do una mano.";

    [TextArea(2, 4)]
    public string completedDialogue = "Tieni. Potrebbe servirti.";

    [TextArea(2, 4)]
    public string repeatDialogue = "Non ho altro da darti.";

    [Header("Trade System (Fase 7)")]
    [Tooltip("Se true, questo studente e' legato a un baratto e non puo' sparire finche' non l'ha fatto.")]
    public bool essential = false;

    [Tooltip("Diventa true quando lo scambio e' stato completato.")]
    public bool tradeDone = false;

    [Tooltip("Lascia vuoto se questo studente regala subito l'oggetto.")]
    public string requiredItem = "";

    [Tooltip("Oggetto ricevuto dal player al termine del dialogo/baratto.")]
    public string rewardItem = "";

    [Tooltip("Se true, l'oggetto richiesto viene consumato nel baratto.")]
    public bool consumeRequiredItem = true;

    [Header("State")]
    [SerializeField] private bool isVisible = true;

    private MeshRenderer bodyRenderer;
    private Material bodyMaterialInstance;

    void Awake()
    {
        bodyRenderer = GetComponentInChildren<MeshRenderer>();
        if (bodyRenderer != null)
        {
            bodyMaterialInstance = bodyRenderer.material;
            bodyMaterialInstance.color = studentColor;
        }
    }

    /// <summary>
    /// Puo' sparire? Solo se non e' essential, oppure se e' essential ma ha gia' fatto il baratto.
    /// </summary>
    public bool CanDisappear()
    {
        return isVisible && (!essential || tradeDone);
    }

    public bool IsVisible => isVisible;

    public void Disappear()
    {
        isVisible = false;
        gameObject.SetActive(false);
        Debug.Log($"[Student] {DisplayName} e' sparito...");
    }

    public void Reappear()
    {
        isVisible = true;
        gameObject.SetActive(true);
    }

    public bool CanInteract(EscapeInventory inventory)
    {
        return isVisible;
    }

    public string GetInteractionPrompt(EscapeInventory inventory)
    {
        return $"[F] Parla con {DisplayName}";
    }

    public string Interact(EscapeInventory inventory, GameManager gameManager)
    {
        if (inventory == null)
        {
            return BuildDialogue("Non so dove metterti gli oggetti.");
        }

        if (tradeDone)
        {
            string repeatLine = string.IsNullOrWhiteSpace(repeatDialogue)
                ? "Non ho altro da darti."
                : repeatDialogue;
            return BuildDialogue(repeatLine);
        }

        if (NeedsRequiredItem() && !inventory.HasItem(requiredItem))
        {
            string missingLine = string.IsNullOrWhiteSpace(missingItemDialogue)
                ? $"Se trovi {requiredItem}, te lo scambio."
                : missingItemDialogue;
            return BuildDialogue(missingLine);
        }

        if (NeedsRequiredItem() && consumeRequiredItem)
        {
            inventory.RemoveItem(requiredItem);
        }

        tradeDone = true;

        string completionLine = string.IsNullOrWhiteSpace(completedDialogue)
            ? "Tieni. Potrebbe servirti."
            : completedDialogue;

        if (!string.IsNullOrWhiteSpace(rewardItem))
        {
            string inventoryUpdate = inventory.AddRawItem(rewardItem);
            return $"{BuildDialogue(completionLine)}\nRicevuto: {rewardItem}\n{inventoryUpdate}";
        }

        return BuildDialogue(completionLine);
    }

    private bool NeedsRequiredItem()
    {
        return !string.IsNullOrWhiteSpace(requiredItem);
    }

    private string BuildDialogue(string mainLine)
    {
        string intro = string.IsNullOrWhiteSpace(openingDialogue)
            ? string.Empty
            : $"{DisplayName}: \"{openingDialogue}\"";

        string body = $"{DisplayName}: \"{mainLine}\"";

        if (string.IsNullOrWhiteSpace(intro))
        {
            return body;
        }

        return $"{intro}\n{body}";
    }

    private string DisplayName
    {
        get
        {
            return string.IsNullOrWhiteSpace(studentName) ? gameObject.name : studentName;
        }
    }
}
