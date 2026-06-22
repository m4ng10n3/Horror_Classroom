using System;
using System.Collections.Generic;
using UnityEngine;

public class PhysicalInventory : MonoBehaviour
{
    public static PhysicalInventory Instance { get; private set; }

    [Header("Capacità")]
    [Min(1)]
    [Tooltip("Numero massimo di oggetti che il giocatore può portare contemporaneamente.")]
    public int maxCapacity = 2;

    private readonly List<CollectedItem> items = new List<CollectedItem>();
    public IReadOnlyList<CollectedItem> Items => items;
    public int Count => items.Count;
    public bool IsFull => items.Count >= maxCapacity;

    public event Action<CollectedItem> OnItemAdded;
    public event Action<CollectedItem> OnItemRemoved;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool AddItem(CollectedItem item)
    {
        if (item == null || IsFull)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.name))
        {
            item.name = "Oggetto";
        }

        items.Add(item);
        OnItemAdded?.Invoke(item);
        return true;
    }

    public bool HasItem(string itemName)
    {
        return IndexOf(itemName) >= 0;
    }

    public bool HasItemId(string inventoryId)
    {
        return IndexOfId(inventoryId) >= 0;
    }

    public bool RemoveItem(string itemName)
    {
        int index = IndexOf(itemName);
        if (index < 0)
        {
            return false;
        }

        return RemoveAt(index, true);
    }

    public bool RemoveItem(CollectedItem item, bool destroyWorldSource = true)
    {
        if (item == null)
        {
            return false;
        }

        int index = items.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        return RemoveAt(index, destroyWorldSource);
    }

    private bool RemoveAt(int index, bool destroyWorldSource)
    {
        CollectedItem removedItem = items[index];
        items.RemoveAt(index);

        if (destroyWorldSource && removedItem.worldSource != null)
        {
            Destroy(removedItem.worldSource);
        }

        OnItemRemoved?.Invoke(removedItem);
        return true;
    }

    public string GetItemsSummary()
    {
        if (items.Count == 0)
        {
            return "vuoto";
        }

        List<string> names = new List<string>();
        for (int i = 0; i < items.Count; i++)
        {
            string itemName = items[i] != null ? items[i].name : null;
            names.Add(string.IsNullOrWhiteSpace(itemName) ? "Oggetto" : itemName.Trim());
        }

        return string.Join(", ", names);
    }

    private int IndexOf(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return -1;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && SameItem(items[i].name, itemName))
            {
                return i;
            }
        }

        return -1;
    }

    private int IndexOfId(string inventoryId)
    {
        if (string.IsNullOrWhiteSpace(inventoryId))
        {
            return -1;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && SameItem(items[i].inventoryId, inventoryId))
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
