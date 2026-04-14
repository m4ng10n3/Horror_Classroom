using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class WindowManager : MonoBehaviour
{
    [Header("Windows")]
    public List<GameObject> allWindows = new List<GameObject>();

    public int VisibleCount
    {
        get
        {
            int count = 0;
            foreach (var w in allWindows)
                if (w != null && w.activeSelf) count++;
            return count;
        }
    }

    public bool DisappearRandomWindow()
    {
        List<GameObject> active = new List<GameObject>();
        foreach (var w in allWindows)
            if (w != null && w.activeSelf) active.Add(w);

        if (active.Count == 0) return false;

        GameObject victim = active[Random.Range(0, active.Count)];
        victim.SetActive(false);
        Debug.Log($"[WindowManager] Finestra scomparsa: {victim.name}");
        return true;
    }

    public bool AppearRandomWindow()
    {
        List<GameObject> hidden = new List<GameObject>();
        foreach (var w in allWindows)
            if (w != null && !w.activeSelf) hidden.Add(w);

        if (hidden.Count == 0) return false;

        GameObject w2 = hidden[Random.Range(0, hidden.Count)];
        w2.SetActive(true);
        Debug.Log($"[WindowManager] Finestra apparsa: {w2.name}");
        return true;
    }
}