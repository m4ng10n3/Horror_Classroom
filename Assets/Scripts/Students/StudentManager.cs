using System.Collections.Generic;
using UnityEngine;
using System;

public class StudentManager : MonoBehaviour
{
    [Header("Students")]
    [Tooltip("Trascina qui tutti i 7 StudentNPC della scena")]
    public List<StudentNPC> allStudents = new List<StudentNPC>();

    // Evento notifica quando uno studente sparisce
    public event Action<StudentNPC> OnStudentDisappeared;
    public event Action OnAllStudentsGone;

    /// <summary>
    /// Conta gli studenti ancora visibili
    /// </summary>
    public int VisibleCount
    {
        get
        {
            int count = 0;
            foreach (var s in allStudents)
                if (s != null && s.IsVisible) count++;
            return count;
        }
    }

    /// <summary>
    /// Fa sparire uno studente casuale tra quelli che possono sparire.
    /// Ritorna true se ne ha fatto sparire uno, false se nessuno disponibile.
    /// </summary>
    public bool DisappearRandomStudent()
    {
        List<StudentNPC> candidates = new List<StudentNPC>();
        foreach (var s in allStudents)
        {
            if (s != null && s.CanDisappear())
                candidates.Add(s);
        }

        if (candidates.Count == 0)
        {
            Debug.Log("[StudentManager] Nessuno studente può sparire");
            return false;
        }

        StudentNPC victim = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        victim.Disappear();
        OnStudentDisappeared?.Invoke(victim);

        // Controlla se sono rimasti tutti spariti
        if (VisibleCount == 0)
        {
            Debug.Log("[StudentManager] TUTTI GLI STUDENTI SONO SPARITI");
            OnAllStudentsGone?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Ritorna la lista degli studenti ancora visibili
    /// </summary>
    public List<StudentNPC> GetVisibleStudents()
    {
        List<StudentNPC> visible = new List<StudentNPC>();
        foreach (var s in allStudents)
            if (s != null && s.IsVisible) visible.Add(s);
        return visible;
    }
}