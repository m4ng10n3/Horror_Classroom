using UnityEngine;

public class DoorEscape : MonoBehaviour, IPlayerInteractable
{
    [Header("Door Settings")]
    public int requiredCraftedItems = 3;
    public string victoryMessage = "SEI SCAPPATO DALLA CLASSE.";

    [TextArea(2, 4)]
    public string lockedDialogue = "La porta e' bloccata. Mi servono ancora pezzi utili per aprirla.";

    [TextArea(2, 4)]
    public string unlockedDialogue = "La maniglia cede. Questa e' la mia occasione.";

    private bool used = false;

    public bool CanInteract(EscapeInventory inventory)
    {
        return true;
    }

    public string GetInteractionPrompt(EscapeInventory inventory)
    {
        if (inventory != null && inventory.CraftedItemCount >= requiredCraftedItems)
        {
            return "[F] Usa la porta";
        }

        return "[F] Controlla la porta";
    }

    public string Interact(EscapeInventory inventory, GameManager gameManager)
    {
        if (used)
        {
            return unlockedDialogue;
        }

        if (inventory == null)
        {
            return "Non so quanti pezzi ho raccolto.";
        }

        int missing = requiredCraftedItems - inventory.CraftedItemCount;
        if (missing > 0)
        {
            return $"{lockedDialogue}\nPezzi craftati: {inventory.CraftedItemCount}/{requiredCraftedItems}";
        }

        used = true;

        if (gameManager != null)
        {
            gameManager.TriggerVictory(victoryMessage);
        }

        return unlockedDialogue;
    }
}
