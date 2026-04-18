using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public TeacherController teacher;
    public TeacherStateMachine teacherStateMachine;
    public FPSController player;
    public GameObject questionPanel;
    public TextMeshProUGUI questionText;
    public Button[] answerButtons = new Button[4];

    [Header("Question System")]
    public QuestionDatabase questionDatabase;
    public SuspicionCounter suspicionCounter;

    [Header("Exploration Window")]
    public float explorationDuration = 10f;
    public float warningStartTime = 3.5f;
    public VignetteController vignetteController;

    private float explorationTimer = 0f;
    private bool playerCaughtStanding = false;

    [Header("Students & Environment")]
    public StudentManager studentManager;
    public ClassroomMutator classroomMutator;
    public WindowManager windowManager;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;
    public Button restartButton;

    [Header("Timing")]
    public float delayBeforeFirstQuestion = 2f;
    public float resultDisplayTime = 2f;
    public float betweenQuestionsPause = 3f;

    // Stato
    private Question currentQuestion;
    private float questionTimer = 0f;
    private float questionTimeLimit = 0f;
    private enum GameState { Waiting, AskingQuestion, ShowingResult, BetweenQuestions, ExplorationWindow }
    private GameState state = GameState.Waiting;
    private bool isGameOver = false;

    void Start()
    {
        HideQuestionPanel();

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int capturedIndex = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerClicked(capturedIndex));
        }

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (player != null)
        {
            player.OnPlayerStoodUp += OnPlayerStoodUp;
            player.OnPlayerSatDown += OnPlayerSatDown;
        }

        StartCoroutine(InitialDelay());
    }

    void Update()
    {
        if (isGameOver) return;

        HandleKeyboardInput();

        if (state == GameState.AskingQuestion)
        {
            questionTimer -= Time.deltaTime;
            if (questionTimer <= 0f)
                OnAnswerClicked(-1);
        }

        if (state == GameState.ExplorationWindow)
        {
            explorationTimer -= Time.deltaTime;

            // Attiva pulsazione vignette negli ultimi secondi
            if (explorationTimer <= warningStartTime && vignetteController != null)
            {
                float urgency = 1f - (explorationTimer / warningStartTime);
                float pulseSpeed = Mathf.Lerp(2f, 8f, urgency);
                float pulseIntensity = Mathf.Lerp(0.1f, 0.5f, urgency);
                vignetteController.StartPulsing(pulseSpeed, pulseIntensity);
            }

            // Tempo scaduto: la prof si gira
            if (explorationTimer <= 0f)
            {
                EndExplorationWindow();
            }
        }
    }

    void HandleKeyboardInput()
    {
        if (state != GameState.AskingQuestion) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) OnAnswerClicked(0);
        else if (kb.digit2Key.wasPressedThisFrame) OnAnswerClicked(1);
        else if (kb.digit3Key.wasPressedThisFrame) OnAnswerClicked(2);
        else if (kb.digit4Key.wasPressedThisFrame) OnAnswerClicked(3);
    }

    IEnumerator InitialDelay()
    {
        yield return new WaitForSeconds(delayBeforeFirstQuestion);
        ShowNextQuestion();
    }

    void ShowNextQuestion()
    {
        if (questionDatabase == null)
        {
            Debug.LogError("[GameManager] QuestionDatabase non assegnato!");
            return;
        }

        QuestionCategory category = QuestionCategory.Scholastic;
        if (teacherStateMachine != null)
        {
            switch (teacherStateMachine.CurrentState)
            {
                case TeacherState.Neutral:
                    category = QuestionCategory.Scholastic;
                    break;
                case TeacherState.Pleased:
                    category = QuestionCategory.CursedEnvironmental;
                    break;
                case TeacherState.Angry:
                    category = QuestionCategory.Aggressive;
                    break;
            }
        }

        currentQuestion = questionDatabase.GetRandomQuestion(category);
        if (currentQuestion == null)
        {
            Debug.LogWarning($"[GameManager] Nessuna domanda per categoria {category}, fallback Scholastic");
            currentQuestion = questionDatabase.GetRandomQuestion(QuestionCategory.Scholastic);
            if (currentQuestion == null)
            {
                Debug.LogError("[GameManager] Nessuna domanda disponibile nel database!");
                return;
            }
        }

        questionText.text = currentQuestion.questionText;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            TextMeshProUGUI btnText = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            btnText.text = (i + 1) + ". " + currentQuestion.options[i];
            answerButtons[i].interactable = true;
        }

        questionTimeLimit = currentQuestion.timeLimit;
        questionTimer = currentQuestion.timeLimit;

        ShowQuestionPanel();
        if (teacher != null) teacher.FaceClass();
        if (player != null) player.forceSeated = true;
        state = GameState.AskingQuestion;
    }

    void OnAnswerClicked(int index)
    {
        if (state != GameState.AskingQuestion) return;
        if (currentQuestion == null) return;
        state = GameState.ShowingResult;

        int effectiveCorrect = GetEffectiveCorrectIndex(currentQuestion);
        bool correct = index == effectiveCorrect;

        if (index == -1)
        {
            questionText.text = "\"Troppo lento...\"";
            if (teacherStateMachine != null) teacherStateMachine.RegisterWrongAnswer();
        }
        else if (correct)
        {
            string feedback = string.IsNullOrEmpty(currentQuestion.customCorrectFeedback)
                ? "\"Bene.\""
                : currentQuestion.customCorrectFeedback;
            questionText.text = feedback;
            if (teacherStateMachine != null) teacherStateMachine.RegisterCorrectAnswer();

            if (currentQuestion.category == QuestionCategory.CursedEnvironmental
                && suspicionCounter != null)
            {
                suspicionCounter.Increase(1, "cursed question correct");
            }
        }
        else
        {
            string feedback = string.IsNullOrEmpty(currentQuestion.customWrongFeedback)
                ? "\"Sbagliato.\""
                : currentQuestion.customWrongFeedback;
            questionText.text = feedback;
            if (teacherStateMachine != null) teacherStateMachine.RegisterWrongAnswer();
        }

        foreach (var btn in answerButtons) btn.interactable = false;

        if (isGameOver) return;

        StartCoroutine(AfterResult());
    }

    IEnumerator AfterResult()
    {
        yield return new WaitForSeconds(resultDisplayTime);
        HideQuestionPanel();

        // La prof si gira alla lavagna
        if (teacher != null) teacher.FaceBoard();
        if (player != null) player.forceSeated = false;

        // Aspetta che la prof abbia completato la rotazione verso la lavagna
        yield return new WaitForSeconds(1.2f);

        // Inizia la finestra di esplorazione
        StartExplorationWindow();
    }

    void OnPlayerStoodUp()
    {
        if (isGameOver) return;
        if (studentManager == null) return;

        studentManager.DisappearRandomStudent();

        if (studentManager.VisibleCount == 0)
        {
            TriggerGameOver("SEI RIMASTO SOLO.");
        }
    }

    void OnPlayerSatDown()
    {
        if (isGameOver) return;
        if (classroomMutator == null) return;

        if (suspicionCounter != null && suspicionCounter.ShouldMutate)
        {
            classroomMutator.ApplyRandomMutation();
        }
    }

    void StartExplorationWindow()
    {
        state = GameState.ExplorationWindow;
        explorationTimer = explorationDuration;
        playerCaughtStanding = false;
        Debug.Log($"[GM] Finestra esplorazione aperta: {explorationDuration} secondi");
    }

    void EndExplorationWindow()
    {
        state = GameState.Waiting;

        // Ferma la pulsazione della vignette
        if (vignetteController != null)
            vignetteController.StopPulsing();

        // La prof si gira verso la classe
        if (teacher != null) teacher.FaceClass();

        // Controlla se il player è in piedi dopo un breve delay (tempo della rotazione)
        StartCoroutine(CheckPlayerAfterRotation());
    }

    IEnumerator CheckPlayerAfterRotation()
    {
        // Aspetta che la prof completi la rotazione verso la classe (~1 sec)
        yield return new WaitForSeconds(1.2f);

        if (isGameOver) yield break;

        // Se il player è ancora in piedi → MORTE
        if (player != null && !player.isSeated)
        {
            playerCaughtStanding = true;
            TriggerGameOver("TI HO VISTO ALZARTI.");
            yield break;
        }

        // Sopravvissuto: il sospetto cresce
        if (suspicionCounter != null)
        {
            suspicionCounter.Increase(1, "sopravvissuto alla finestra");
        }

        Debug.Log("[GM] Finestra chiusa, player al sicuro");

        // Breve pausa poi prossima domanda
        yield return new WaitForSeconds(1f);
        ShowNextQuestion();
    }

    int GetEffectiveCorrectIndex(Question q)
    {
        if (q == null) return 0;

        if (q.environmentCheck == EnvironmentCheckType.None)
            return q.correctIndex;

        int realValue = GetEnvironmentValue(q.environmentCheck);
        if (realValue < 0) return q.correctIndex;

        int matchIndex = FindMatchingOption(q.options, realValue);

        if (matchIndex >= 0)
        {
            Debug.Log($"[GM] Environment check {q.environmentCheck}={realValue} → risposta corretta = opzione {matchIndex}");
            return matchIndex;
        }

        Debug.Log($"[GM] Environment check {q.environmentCheck}={realValue} → nessuna opzione corrisponde, fallback {q.fallbackCorrectIndex}");
        return q.fallbackCorrectIndex;
    }

    int GetEnvironmentValue(EnvironmentCheckType type)
    {
        switch (type)
        {
            case EnvironmentCheckType.WindowsCount:
                return windowManager != null ? windowManager.VisibleCount : -1;
            case EnvironmentCheckType.StudentsCount:
                return studentManager != null ? studentManager.VisibleCount : -1;
            case EnvironmentCheckType.EmptyDesksCount:
                if (studentManager == null) return -1;
                return studentManager.allStudents.Count - studentManager.VisibleCount;
            default:
                return -1;
        }
    }

    int FindMatchingOption(string[] options, int value)
    {
        string[] wordNumbers = { "zero", "uno", "due", "tre", "quattro", "cinque",
                                 "sei", "sette", "otto", "nove", "dieci" };
        string valueWord = (value >= 0 && value < wordNumbers.Length) ? wordNumbers[value] : null;
        string valueNumeric = value.ToString();

        for (int i = 0; i < options.Length; i++)
        {
            string opt = options[i].Trim().ToLower();
            if (opt == valueNumeric) return i;
            if (valueWord != null && opt == valueWord) return i;
        }
        return -1;
    }

    void ShowQuestionPanel()
    {
        if (questionPanel != null) questionPanel.SetActive(true);
    }

    void HideQuestionPanel()
    {
        if (questionPanel != null) questionPanel.SetActive(false);
    }

    public void TriggerGameOverExternal(string message)
    {
        TriggerGameOver(message);
    }

    void TriggerGameOver(string message)
    {
        isGameOver = true;
        state = GameState.Waiting;
        HideQuestionPanel();

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverText != null) gameOverText.text = message;

        if (player != null)
        {
            player.forceSeated = true;
            player.gameplayFrozen = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StopAllCoroutines();
    }

    void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}