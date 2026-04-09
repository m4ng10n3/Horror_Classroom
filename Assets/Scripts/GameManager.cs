using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    [System.Serializable]
    public class Question
    {
        public string text;
        public string[] options = new string[4];
        public int correctIndex;
        public float timeLimit = 12f;
    }

    [Header("References")]
    public TeacherController teacher;
    public TeacherStateMachine teacherStateMachine;
    public FPSController player;
    public GameObject questionPanel;
    public TextMeshProUGUI questionText;
    public Button[] answerButtons = new Button[4];

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;
    public Button restartButton;

    [Header("Timing")]
    public float delayBeforeFirstQuestion = 2f;
    public float resultDisplayTime = 2f;
    public float betweenQuestionsPause = 3f;

    // Stato
    private List<Question> questions;
    private int currentQuestionIndex = 0;
    private float questionTimer = 0f;
    private float questionTimeLimit = 0f;
    private enum GameState { Waiting, AskingQuestion, ShowingResult, BetweenQuestions }
    private GameState state = GameState.Waiting;
    private bool isGameOver = false;

    void Start()
    {
        BuildQuestionList();
        HideQuestionPanel();

        // Aggancia i listener ai pulsanti risposta
        for (int i = 0; i < answerButtons.Length; i++)
        {
            int capturedIndex = i;
            answerButtons[i].onClick.AddListener(() => OnAnswerClicked(capturedIndex));
        }

        // Setup game over UI
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
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
            {
                OnAnswerClicked(-1); // timeout
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
        if (currentQuestionIndex >= questions.Count)
        {
            currentQuestionIndex = 0; // loop per ora
        }

        Question q = questions[currentQuestionIndex];
        questionText.text = q.text;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            TextMeshProUGUI btnText = answerButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            btnText.text = (i + 1) + ". " + q.options[i];
            answerButtons[i].interactable = true;
        }

        questionTimeLimit = q.timeLimit;
        questionTimer = q.timeLimit;

        ShowQuestionPanel();
        if (teacher != null) teacher.FaceClass();
        if (player != null) player.forceSeated = true;
        state = GameState.AskingQuestion;
    }

    void OnAnswerClicked(int index)
    {
        if (state != GameState.AskingQuestion) return;
        state = GameState.ShowingResult;

        Question q = questions[currentQuestionIndex];
        bool correct = index == q.correctIndex;

        if (index == -1)
        {
            questionText.text = "\"Troppo lento...\"";
            if (teacherStateMachine != null) teacherStateMachine.RegisterWrongAnswer();
        }
        else if (correct)
        {
            questionText.text = "\"Bene.\"";
            if (teacherStateMachine != null) teacherStateMachine.RegisterCorrectAnswer();
        }
        else
        {
            questionText.text = "\"Sbagliato.\"";
            if (teacherStateMachine != null) teacherStateMachine.RegisterWrongAnswer();
        }

        foreach (var btn in answerButtons) btn.interactable = false;

        // Se la state machine ha triggerato game over, non proseguire
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
        currentQuestionIndex++;
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

    void BuildQuestionList()
    {
        questions = new List<Question>
        {
            new Question {
                text = "\"Bene, bambini. Quanto fa 7 per 8?\"",
                options = new[] { "54", "56", "58", "48" },
                correctIndex = 1, timeLimit = 15f
            },
            new Question {
                text = "\"Qual è la capitale della Francia?\"",
                options = new[] { "Londra", "Berlino", "Parigi", "Madrid" },
                correctIndex = 2, timeLimit = 12f
            },
            new Question {
                text = "\"Quanti studenti ci sono in questa classe? Contate bene...\"",
                options = new[] { "Sei", "Sette", "Otto", "Non ricordo" },
                correctIndex = 1, timeLimit = 10f
            },
            new Question {
                text = "\"Di che colore sono le pareti di questa classe?\"",
                options = new[] { "Bianche", "Beige", "Grigie", "Non le vedo più" },
                correctIndex = 1, timeLimit = 10f
            },
            new Question {
                text = "\"Quante finestre ha questa stanza?\"",
                options = new[] { "Tre", "Quattro", "Cinque", "Non ci sono finestre" },
                correctIndex = 1, timeLimit = 8f
            },
            new Question {
                text = "\"Hai sentito anche tu quel rumore dalla porta?\"",
                options = new[] { "Quale rumore?", "Sì...", "No", "Ho paura" },
                correctIndex = 0, timeLimit = 8f
            },
            new Question {
                text = "\"Quanti banchi vuoti vedi...?\"",
                options = new[] { "Nessuno", "Uno", "Non lo so", "Stanno aumentando" },
                correctIndex = 3, timeLimit = 7f
            },
            new Question {
                text = "\"Chi si siede alla tua sinistra?\"",
                options = new[] { "Paolo", "Marco", "Nessuno", "Non ricordo più" },
                correctIndex = 0, timeLimit = 7f
            },
            new Question {
                text = "\"Sai cosa c'è fuori da quella porta?\"",
                options = new[] { "Il corridoio", "Casa", "Niente", "Non voglio saperlo" },
                correctIndex = 2, timeLimit = 6f
            },
            new Question {
                text = "\"Pensi davvero... di potertene andare?\"",
                options = new[] { "Sì", "No", "...", "Devo provarci" },
                correctIndex = 3, timeLimit = 5f
            }
        };
    }
}