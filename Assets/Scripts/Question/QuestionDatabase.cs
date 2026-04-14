using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestionDatabase", menuName = "Horror Game/Question Database", order = 2)]
public class QuestionDatabase : ScriptableObject
{
    [Header("All Questions")]
    [Tooltip("Trascina qui tutti gli asset Question del progetto")]
    public List<Question> allQuestions = new List<Question>();

    private Dictionary<QuestionCategory, Question> lastAsked = new Dictionary<QuestionCategory, Question>();

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
    /// Ritorna una domanda casuale della categoria, garantendo che non sia
    /// la stessa dell'ultima mostrata in quella categoria.
    /// </summary>
    public Question GetRandomQuestion(QuestionCategory category)
    {
        List<Question> filtered = GetQuestionsByCategory(category);
        if (filtered.Count == 0) return null;

        lastAsked.TryGetValue(category, out Question last);

        // Se c'è solo una domanda non possiamo evitare la ripetizione
        List<Question> candidates = filtered.Count > 1 && last != null
            ? filtered.FindAll(q => q != last)
            : filtered;

        Question picked = candidates[Random.Range(0, candidates.Count)];
        lastAsked[category] = picked;
        return picked;
    }
}