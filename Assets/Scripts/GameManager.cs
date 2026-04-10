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
    private enum GameState { Waiting, AskingQuestion, ShowingResult, BetweenQuestions }
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

        // Scegli categoria in base allo stato della prof
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

        bool correct = index == currentQuestion.correctIndex;

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
        if (teacher != null) teacher.FaceBoard();
        if (player != null) player.forceSeated = false;
        state = GameState.BetweenQuestions;
        yield return new WaitForSeconds(betweenQuestionsPause);
        ShowNextQuestion();
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