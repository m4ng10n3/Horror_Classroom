using UnityEngine;
using System;

public class TeacherStateMachine : MonoBehaviour
{
    [Header("Current State")]
    [SerializeField] private TeacherState currentState = TeacherState.Neutral;

    [Header("Transition Thresholds")]
    [Tooltip("Risposte giuste consecutive per passare da Neutral a Pleased")]
    public int pointsToPleased = 2;
    [Tooltip("Risposte sbagliate consecutive per passare da Neutral a Angry")]
    public int pointsToAngry = 2;
    [Tooltip("Risposte giuste per uscire da Angry e tornare a Neutral")]
    public int recoveryPoints = 2;
    [Tooltip("Risposte giuste in Pleased prima che scatti la 'finestra' (segnaposto Fase 5)")]
    public int pleasedTriggerPoints = 2;

    // Contatori (si resettano ad ogni transizione)
    private int positivePoints = 0;
    private int negativePoints = 0;
    private int recoveryCount = 0;
    private int pleasedCount = 0;

    // Evento per notificare chi è interessato del cambio stato
    public event Action<TeacherState, TeacherState> OnStateChanged;

    public TeacherState CurrentState => currentState;

    void Start()
    {
        // Notifica lo stato iniziale (così i listener possono settare subito luci/colori)
        OnStateChanged?.Invoke(currentState, currentState);
    }

    public void RegisterCorrectAnswer()
    {
        Debug.Log($"[Teacher] Correct answer in state {currentState}");
        switch (currentState)
        {
            case TeacherState.Neutral:
                positivePoints++;
                if (positivePoints >= pointsToPleased)
                    ChangeState(TeacherState.Pleased);
                break;

            case TeacherState.Pleased:
                pleasedCount++;
                if (pleasedCount >= pleasedTriggerPoints)
                {
                    // FASE 5: qui apriremo la finestra esplorazione.
                    // Per ora torniamo a Neutral come segnaposto.
                    Debug.Log("[Teacher] Pleased trigger reached (placeholder: return to Neutral)");
                    ChangeState(TeacherState.Neutral);
                }
                break;

            case TeacherState.Angry:
                recoveryCount++;
                if (recoveryCount >= recoveryPoints)
                    ChangeState(TeacherState.Neutral);
                break;
        }
    }

    public void RegisterWrongAnswer()
    {
        Debug.Log($"[Teacher] Wrong answer in state {currentState}");
        switch (currentState)
        {
            case TeacherState.Neutral:
                negativePoints++;
                if (negativePoints >= pointsToAngry)
                    ChangeState(TeacherState.Angry);
                break;

            case TeacherState.Pleased:
                // Delusione: torna a Neutral senza rabbia
                ChangeState(TeacherState.Neutral);
                break;

            case TeacherState.Angry:
                // FASE 6: qui triggereremo "ultima possibilità".
                // Per ora, game over secco.
                Debug.Log("[Teacher] Wrong answer in Angry ? GAME OVER (placeholder)");
                GameManager gm = FindFirstObjectByType<GameManager>();
                if (gm != null) gm.TriggerGameOverExternal("\"Non meriti di stare qui.\"");
                break;
        }
    }

    private void ChangeState(TeacherState newState)
    {
        if (newState == currentState) return;

        TeacherState oldState = currentState;
        currentState = newState;

        // Reset di tutti i contatori ad ogni transizione
        positivePoints = 0;
        negativePoints = 0;
        recoveryCount = 0;
        pleasedCount = 0;

        Debug.Log($"[Teacher] State change: {oldState} ? {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }

    // Per debug/editor
    public void ForceState(TeacherState newState)
    {
        ChangeState(newState);
    }
}