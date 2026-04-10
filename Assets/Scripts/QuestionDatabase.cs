using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestionDatabase", menuName = "Horror Game/Question Database", order = 2)]
public class QuestionDatabase : ScriptableObject
{
    [Header("All Questions")]
    [Tooltip("Trascina qui tutti gli asset Question del progetto")]
    public List<Question> allQuestions = new List<Question>();

    /// <summary>
    /// Ritorna tutte le domande di una certa categoria
    /// </summary>
    public List<Question> GetQuestionsByCategory(QuestionCategory category)
    {
        List<Question> result = new List<Question>();
        foreach (var q in allQuestions)
        {
            if (q != null && q.category == category)
                result.Add(q);
        }
        return result;
    }

    /// <summary>
    /// Ritorna una domanda casuale della categoria specificata.
    /// Ritorna null se non ci sono domande in quella categoria.
    /// </summary>
    public Question GetRandomQuestion(QuestionCategory category)
    {
        List<Question> filtered = GetQuestionsByCategory(category);
        if (filtered.Count == 0) return null;
        return filtered[Random.Range(0, filtered.Count)];
    }
}