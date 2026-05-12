using UnityEngine;

public class StudentNPC : MonoBehaviour, IPlayerInteractable, IDialogueSequenceInteractable
{
    [Header("Identity")]
    public string studentName = "Studente";
    public Color studentColor = Color.white;

    [Header("Dialogo — Oggetto Mancante")]
    public DialogueLine[] missingItemSequence = new DialogueLine[]
    {
        new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Psst... ho bisogno di qualcosa." },
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Di cosa hai bisogno?" },
        new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Se mi porti l'oggetto giusto, ti do una mano." },
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Ok, ci provo." }
    };

    [Header("Dialogo — Baratto Completato")]
    public DialogueLine[] completedSequence = new DialogueLine[]
    {
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Ho quello che cercavi." },
        new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Perfetto! Tieni, potrebbe servirti." },
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Grazie, ne avevo bisogno." }
    };

    [Header("Dialogo — Baratto Già Fatto")]
    public DialogueLine[] repeatSequence = new DialogueLine[]
    {
        new DialogueLine { speaker = DialogueLine.Speaker.NPC,    text = "Non ho altro da darti." },
        new DialogueLine { speaker = DialogueLine.Speaker.Player, text = "Capisco, grazie comunque." }
    };

    [Header("Trade System")]
    [Tooltip("Se true, questo studente non può sparire finché non ha completato il baratto.")]
    public bool essential = false;

    [Tooltip("Diventa true quando lo scambio è stato completato.")]
    public bool tradeDone = false;

    [Tooltip("Lascia vuoto se lo studente regala subito l'oggetto senza richiederne uno.")]
    public string requiredItem = "";

    [Tooltip("Oggetto ricevuto dal player al termine del baratto.")]
    public string rewardItem = "";

    [Tooltip("Se true, l'oggetto richiesto viene consumato nel baratto.")]
    public bool consumeRequiredItem = true;

    [Header("State")]
    [SerializeField] private bool isVisible = true;

    private MeshRenderer bodyRenderer;
    private Material bodyMaterialInstance;

    public string SpeakerName => string.IsNullOrWhiteSpace(studentName) ? gameObject.name : studentName;

    void Awake()
    {
        bodyRenderer = GetComponentInChildren<MeshRenderer>();
        if (bodyRenderer != null)
        {
            bodyMaterialInstance = bodyRenderer.material;
            bodyMaterialInstance.color = studentColor;
        }
    }

    public bool CanDisappear() => isVisible && (!essential || tradeDone);
    public bool IsVisible => isVisible;

    public void Disappear()
    {
        isVisible = false;
        gameObject.SetActive(false);
        Debug.Log($"[Student] {SpeakerName} è sparito...");
    }

    public void Reappear()
    {
        isVisible = true;
        gameObject.SetActive(true);
    }

    public bool CanInteract(EscapeInventory inventory) => isVisible;

    public string GetInteractionPrompt(EscapeInventory inventory) => $"[F] Parla con {SpeakerName}";

    // Fallback usato solo se IDialogueSequenceInteractable non è supportato dal controller.
    public string Interact(EscapeInventory inventory, GameManager gameManager)
    {
        DialogueLine[] seq = GetDialogueSequence(inventory, gameManager);
        if (seq == null || seq.Length == 0)
            return string.Empty;
        return $"{SpeakerName}: \"{seq[0].text}\"";
    }

    public DialogueLine[] GetDialogueSequence(EscapeInventory inventory, GameManager gameManager)
    {
        if (inventory == null)
        {
            return new[] { new DialogueLine { speaker = DialogueLine.Speaker.NPC, text = "Non so dove metterti gli oggetti." } };
        }

        if (tradeDone)
            return repeatSequence;

        if (NeedsRequiredItem() && !inventory.HasItem(requiredItem))
            return missingItemSequence;

        // Esegui il baratto.
        if (NeedsRequiredItem() && consumeRequiredItem)
            inventory.RemoveItem(requiredItem);

        tradeDone = true;

        if (!string.IsNullOrWhiteSpace(rewardItem))
        {
            inventory.AddRawItem(rewardItem);
            // Aggiungi una riga finale di sistema che comunica l'oggetto ricevuto.
            var extended = new DialogueLine[completedSequence.Length + 1];
            System.Array.Copy(completedSequence, extended, completedSequence.Length);
            extended[completedSequence.Length] = new DialogueLine
            {
                speaker = DialogueLine.Speaker.NPC,
                text = $"[Ricevuto: {rewardItem}]"
            };
            return extended;
        }

        return completedSequence;
    }

    private bool NeedsRequiredItem() => !string.IsNullOrWhiteSpace(requiredItem);
}
