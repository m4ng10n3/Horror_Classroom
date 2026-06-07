using System.Collections.Generic;
using UnityEngine;

public class DoorEscape : MonoBehaviour, IPlayerInteractable
{
    [Header("Door Settings")]
    [Tooltip("Fallback: usato solo se Required Pickup Items e Required Item Names sono vuoti.")]
    [Min(0)]
    public int requiredPhysicalItems = 3;
    public string victoryMessage = "SEI SCAPPATO DALLA CLASSE.";

    [Header("Required Items")]
    [Tooltip("Trascina qui i PickupItem necessari alla fuga. Se questa lista e' compilata, gli altri pickup non contano.")]
    public List<PickupItem> requiredPickupItems = new List<PickupItem>();

    [Tooltip("Nomi opzionali per oggetti non collegati a un PickupItem, per esempio reward dagli studenti.")]
    public List<string> requiredItemNames = new List<string>();

    [Header("Door Text")]
    public string progressLabel = "Oggetti utili";

    [TextArea(2, 4)]
    public string lockedDialogue = "La porta e' bloccata. Mi servono ancora oggetti utili per aprirla.";

    [TextArea(2, 4)]
    public string unlockedDialogue = "La maniglia cede. Questa e' la mia occasione.";

    private bool used = false;

    public bool CanInteract()
    {
        return true;
    }

    public string GetInteractionPrompt()
    {
        PhysicalInventory inventory = ResolvePhysicalInventory();
        if (HasRequiredItems(inventory))
        {
            return "[F] Usa la porta";
        }

        return "[F] Controlla la porta";
    }

    public string Interact(GameManager gameManager)
    {
        if (used)
        {
            return unlockedDialogue;
        }

        PhysicalInventory inventory = ResolvePhysicalInventory();
        if (inventory == null)
        {
            return "Non so quanti oggetti ho raccolto.";
        }

        int requiredCount = GetRequiredTargetCount();
        int collectedCount = GetCollectedRequiredCount(inventory);
        int missing = requiredCount - collectedCount;
        if (missing > 0)
        {
            return $"{lockedDialogue}\n{progressLabel}: {collectedCount}/{requiredCount}";
        }

        used = true;

        if (gameManager != null)
        {
            gameManager.TriggerVictory(victoryMessage);
        }

        return unlockedDialogue;
    }

    private bool HasRequiredItems(PhysicalInventory inventory)
    {
        return inventory != null && GetCollectedRequiredCount(inventory) >= GetRequiredTargetCount();
    }

    private PhysicalInventory ResolvePhysicalInventory()
    {
        return PhysicalInventory.Instance != null
            ? PhysicalInventory.Instance
            : FindFirstObjectByType<PhysicalInventory>();
    }

    public bool UsesSpecificRequiredItems()
    {
        return CountRequiredPickupItems() + CountRequiredItemNames() > 0;
    }

    public int GetRequiredTargetCount()
    {
        int specificCount = CountRequiredPickupItems() + CountRequiredItemNames();
        return specificCount > 0 ? specificCount : requiredPhysicalItems;
    }

    public int GetCollectedRequiredCount(PhysicalInventory inventory)
    {
        if (inventory == null)
        {
            return 0;
        }

        int targetCount = GetRequiredTargetCount();
        if (!UsesSpecificRequiredItems())
        {
            return Mathf.Min(inventory.Count, targetCount);
        }

        int count = 0;
        foreach (PickupItem pickup in requiredPickupItems)
        {
            if (pickup != null && inventory.HasItemId(pickup.GetInventoryId()))
            {
                count++;
            }
        }

        foreach (string itemName in requiredItemNames)
        {
            if (!string.IsNullOrWhiteSpace(itemName) && inventory.HasItem(itemName))
            {
                count++;
            }
        }

        return Mathf.Min(count, targetCount);
    }

    public string GetRequiredItemsSummary(
        PhysicalInventory inventory,
        string emptyText,
        string separator,
        string collectedItemFormat,
        string missingItemFormat,
        bool showMissingItems)
    {
        if (inventory == null)
        {
            return emptyText;
        }

        if (!UsesSpecificRequiredItems())
        {
            return inventory.GetItemsSummary();
        }

        List<string> itemLabels = new List<string>();
        foreach (PickupItem pickup in requiredPickupItems)
        {
            if (pickup == null)
            {
                continue;
            }

            bool collected = inventory.HasItemId(pickup.GetInventoryId());
            AddRequiredItemLabel(itemLabels, pickup.itemName, collected, collectedItemFormat, missingItemFormat, showMissingItems);
        }

        foreach (string itemName in requiredItemNames)
        {
            if (string.IsNullOrWhiteSpace(itemName))
            {
                continue;
            }

            bool collected = inventory.HasItem(itemName);
            AddRequiredItemLabel(itemLabels, itemName.Trim(), collected, collectedItemFormat, missingItemFormat, showMissingItems);
        }

        return itemLabels.Count == 0 ? emptyText : string.Join(separator, itemLabels);
    }

    private void AddRequiredItemLabel(
        List<string> itemLabels,
        string itemName,
        bool collected,
        string collectedItemFormat,
        string missingItemFormat,
        bool showMissingItems)
    {
        if (!collected && !showMissingItems)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(itemName) ? "Oggetto" : itemName.Trim();
        string format = collected ? collectedItemFormat : missingItemFormat;
        itemLabels.Add(string.IsNullOrWhiteSpace(format) ? displayName : string.Format(format, displayName));
    }

    private int CountRequiredPickupItems()
    {
        int count = 0;
        foreach (PickupItem pickup in requiredPickupItems)
        {
            if (pickup != null)
            {
                count++;
            }
        }

        return count;
    }

    private int CountRequiredItemNames()
    {
        int count = 0;
        foreach (string itemName in requiredItemNames)
        {
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                count++;
            }
        }

        return count;
    }
}
