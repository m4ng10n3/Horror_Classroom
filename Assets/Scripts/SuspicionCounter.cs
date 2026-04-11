using UnityEngine;
using System;

public class SuspicionCounter : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Sospetto massimo prima che le cursed questions diventino impossibili")]
    public int maxSuspicion = 5;

    [Tooltip("Soglia oltre la quale partono le mutazioni ambientali")]
    public int mutationThreshold = 1;

    [Tooltip("Soglia oltre la quale le cursed questions diventano trabocchetti")]
    public int trapThreshold = 3;

    [Header("Debug")]
    [SerializeField] private int currentSuspicion = 0;

    public event Action<int, int> OnSuspicionChanged; // (oldValue, newValue)

    public int CurrentSuspicion => currentSuspicion;
    public int MaxSuspicion => maxSuspicion;
    public bool ShouldMutate => currentSuspicion >= mutationThreshold;
    public bool ShouldTrap => currentSuspicion >= trapThreshold;
    public bool IsAtMax => currentSuspicion >= maxSuspicion;

    public void Increase(int amount = 1, string reason = "")
    {
        int oldValue = currentSuspicion;
        currentSuspicion = Mathf.Min(currentSuspicion + amount, maxSuspicion);
        Debug.Log($"[Suspicion] +{amount} ({reason}) ? {currentSuspicion}/{maxSuspicion}");
        OnSuspicionChanged?.Invoke(oldValue, currentSuspicion);
    }

    public void Decrease(int amount = 1, string reason = "")
    {
        int oldValue = currentSuspicion;
        currentSuspicion = Mathf.Max(currentSuspicion - amount, 0);
        Debug.Log($"[Suspicion] -{amount} ({reason}) ? {currentSuspicion}/{maxSuspicion}");
        OnSuspicionChanged?.Invoke(oldValue, currentSuspicion);
    }

    public void Reset()
    {
        int oldValue = currentSuspicion;
        currentSuspicion = 0;
        Debug.Log("[Suspicion] Reset");
        OnSuspicionChanged?.Invoke(oldValue, currentSuspicion);
    }
}