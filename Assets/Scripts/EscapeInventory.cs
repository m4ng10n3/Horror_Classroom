using System;
using System.Collections.Generic;
using UnityEngine;

public class EscapeInventory : MonoBehaviour
{
    [Serializable]
    public class CraftRecipe
    {
        public string ingredientA;
        public string ingredientB;
        public string resultItem;
    }

    [Header("Recipes")]
    public List<CraftRecipe> recipes = new List<CraftRecipe>();

    [Header("Inventory Debug")]
    [SerializeField] private List<string> rawItems = new List<string>();
    [SerializeField] private List<string> craftedItems = new List<string>();

    public IReadOnlyList<string> RawItems => rawItems;
    public IReadOnlyList<string> CraftedItems => craftedItems;
    public int CraftedItemCount => craftedItems.Count;

    void Reset()
    {
        EnsureDefaultRecipes();
    }

    void Awake()
    {
        EnsureDefaultRecipes();
    }

    public bool HasItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return false;
        }

        return IndexOf(rawItems, itemName) >= 0 || IndexOf(craftedItems, itemName) >= 0;
    }

    public bool RemoveItem(string itemName)
    {
        if (TryRemove(rawItems, itemName))
        {
            return true;
        }

        return TryRemove(craftedItems, itemName);
    }

    public string AddRawItem(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return "Oggetto non valido.";
        }

        rawItems.Add(itemName.Trim());

        List<string> craftedNow = new List<string>();
        TryCraftAllAvailable(craftedNow);

        if (craftedNow.Count == 0)
        {
            return $"Inventario oggetti: {GetRawItemsSummary()}";
        }

        return $"Craft completati: {string.Join(", ", craftedNow)}";
    }

    public string GetRawItemsSummary()
    {
        return rawItems.Count == 0 ? "vuoto" : string.Join(", ", rawItems);
    }

    public string GetCraftedItemsSummary()
    {
        return craftedItems.Count == 0 ? "nessuno" : string.Join(", ", craftedItems);
    }

    private void EnsureDefaultRecipes()
    {
        if (recipes.Count > 0)
        {
            return;
        }

        recipes.Add(new CraftRecipe
        {
            ingredientA = "Filo",
            ingredientB = "Nastro adesivo",
            resultItem = "Maniglia improvvisata"
        });

        recipes.Add(new CraftRecipe
        {
            ingredientA = "Righello",
            ingredientB = "Graffetta",
            resultItem = "Leva sottile"
        });

        recipes.Add(new CraftRecipe
        {
            ingredientA = "Batteria",
            ingredientB = "Lampadina",
            resultItem = "Segnalatore di emergenza"
        });
    }

    private void TryCraftAllAvailable(List<string> craftedNow)
    {
        bool craftedSomething;

        do
        {
            craftedSomething = false;

            foreach (CraftRecipe recipe in recipes)
            {
                if (!IsRecipeValid(recipe) || !CanCraft(recipe))
                {
                    continue;
                }

                ConsumeIngredients(recipe);

                string result = recipe.resultItem.Trim();
                craftedItems.Add(result);
                craftedNow.Add($"{result} ({recipe.ingredientA} + {recipe.ingredientB})");
                craftedSomething = true;
                break;
            }
        }
        while (craftedSomething);
    }

    private bool CanCraft(CraftRecipe recipe)
    {
        string ingredientA = recipe.ingredientA.Trim();
        string ingredientB = recipe.ingredientB.Trim();

        if (SameItem(ingredientA, ingredientB))
        {
            return CountOf(rawItems, ingredientA) >= 2;
        }

        return CountOf(rawItems, ingredientA) >= 1 && CountOf(rawItems, ingredientB) >= 1;
    }

    private void ConsumeIngredients(CraftRecipe recipe)
    {
        string ingredientA = recipe.ingredientA.Trim();
        string ingredientB = recipe.ingredientB.Trim();

        TryRemove(rawItems, ingredientA);
        TryRemove(rawItems, ingredientB);
    }

    private bool IsRecipeValid(CraftRecipe recipe)
    {
        return recipe != null
            && !string.IsNullOrWhiteSpace(recipe.ingredientA)
            && !string.IsNullOrWhiteSpace(recipe.ingredientB)
            && !string.IsNullOrWhiteSpace(recipe.resultItem);
    }

    private int CountOf(List<string> items, string itemName)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (SameItem(items[i], itemName))
            {
                count++;
            }
        }

        return count;
    }

    private bool TryRemove(List<string> items, string itemName)
    {
        int index = IndexOf(items, itemName);
        if (index < 0)
        {
            return false;
        }

        items.RemoveAt(index);
        return true;
    }

    private int IndexOf(List<string> items, string itemName)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (SameItem(items[i], itemName))
            {
                return i;
            }
        }

        return -1;
    }

    private bool SameItem(string left, string right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
