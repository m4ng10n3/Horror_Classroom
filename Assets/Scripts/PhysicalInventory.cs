using System;
using System.Collections.Generic;
using UnityEngine;

public class PhysicalInventory : MonoBehaviour
{
    public static PhysicalInventory Instance { get; private set; }

    private readonly List<CollectedItem> items = new List<CollectedItem>();
    public IReadOnlyList<CollectedItem> Items => items;

    public event Action<CollectedItem> OnItemAdded;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddItem(CollectedItem item)
    {
        items.Add(item);
        OnItemAdded?.Invoke(item);
    }
}
