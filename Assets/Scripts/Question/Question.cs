using UnityEngine;

[CreateAssetMenu(fileName = "NewQuestion", menuName = "Horror Game/Question", order = 1)]
public class Question : ScriptableObject
{
    [Header("Content")]
    [TextArea(2, 4)]
    public string questionText;

    public string[] options = new string[4];

    [Range(0, 3)]
    public int correctIndex;

    [Header("Settings")]
    [Tooltip("Secondi per rispondere")]
    public float timeLimit = 12f;

    [Tooltip("In quale stato della prof viene usata questa domanda")]
    public QuestionCategory category;

    [Header("Optional Flavor")]
    [Tooltip("Testo mostrato se il player risponde giusto (vuoto = default \"Bene.\")")]
    public string customCorrectFeedback;

    [Tooltip("Testo mostrato se il player risponde sbagliato (vuoto = default \"Sbagliato.\")")]
    public string customWrongFeedback;

    [Header("Environment Check (cursed questions)")]
    [Tooltip("Se diverso da None, la risposta giusta è calcolata a runtime in base allo stato dell'aula")]
    public EnvironmentCheckType environmentCheck = EnvironmentCheckType.None;

    [Tooltip("Opzione di fallback quando nessuna delle 4 risposte corrisponde al valore reale (es. 'Non ricordo')")]
    [Range(0, 3)]
    public int fallbackCorrectIndex = 3;
}