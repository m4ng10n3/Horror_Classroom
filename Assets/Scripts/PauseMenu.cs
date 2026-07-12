using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

// Menu di pausa costruito interamente da codice.
// - Si apre/chiude con ESC.
// - Da aperto ferma il tempo (Time.timeScale = 0) e mostra il cursore.
// - Contiene la scritta "MENU" e tre bottoni: "Riprendi", "Opzioni" ed "Esci dal gioco".
// - "Opzioni" apre un sottomenu con due slider: volume e sensibilità del mouse.
//   I valori vengono salvati in PlayerPrefs e riapplicati all'avvio.
//
// Basta aggiungere questo componente a un GameObject vuoto in scena.
// I riferimenti a font e sprite si auto-popolano in editor (vedi Reset()).
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
    // Chiavi PlayerPrefs
    private const string PrefVolume = "Options_Volume";
    private const string PrefSensitivity = "Options_MouseSensitivity";

    [Header("Font & Sprite (auto-caricati in editor)")]
    [Tooltip("Font della scritta centrale MENU (Brown Cookies SDF)")]
    public TMP_FontAsset menuFont;
    [Tooltip("Font dei bottoni (Huge Smile SDF)")]
    public TMP_FontAsset buttonFont;
    [Tooltip("Sprite di sfondo dei bottoni (lowpolypixelpaper)")]
    public Sprite buttonSprite;

    [Header("Player")]
    [Tooltip("Se assegnato, il player viene congelato mentre il menu è aperto (niente rotazione camera). Se vuoto viene cercato in scena.")]
    public FPSController player;

    [Header("Opzioni - limiti degli slider")]
    [Tooltip("Sensibilità mouse minima (estremo sinistro dello slider)")]
    public float minSensitivity = 0.02f;
    [Tooltip("Sensibilità mouse massima (estremo destro dello slider)")]
    public float maxSensitivity = 0.5f;

    private GameObject menuRoot;      // pannello a schermo intero, attivato/disattivato
    private GameObject mainPanel;     // MENU + i tre bottoni
    private GameObject optionsPanel;  // sottomenu OPZIONI con i due slider
    private Slider volumeSlider;
    private Slider sensitivitySlider;
    private TextMeshProUGUI volumeValueLabel;
    private TextMeshProUGUI sensitivityValueLabel;
    private bool isOpen;
    private bool optionsOpen;
    private float previousTimeScale = 1f;

    void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<FPSController>();

        EnsureEventSystem();
        BuildUI();
        LoadSettings();
        SetOpen(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame)
            return;

        // Da dentro le opzioni, ESC torna al menu principale invece di chiudere tutto.
        if (isOpen && optionsOpen)
            ShowOptions(false);
        else
            Toggle();
    }

    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    // Chiamato dal bottone "Riprendi".
    public void Resume()
    {
        SetOpen(false);
    }

    // Chiamato dal bottone "Esci dal gioco".
    public void QuitGame()
    {
        // Ripristina il tempo prima di uscire, per sicurezza.
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ----------------------------------------------------------------------
    //  Opzioni
    // ----------------------------------------------------------------------

    // Chiamato dal bottone "Opzioni" (true) e dal bottone "Indietro" (false).
    private void ShowOptions(bool show)
    {
        optionsOpen = show;

        if (mainPanel != null) mainPanel.SetActive(!show);
        if (optionsPanel != null) optionsPanel.SetActive(show);

        if (!show)
            PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefVolume, AudioListener.volume));
        float sensitivity = PlayerPrefs.GetFloat(
            PrefSensitivity, player != null ? player.mouseSensitivity : minSensitivity);
        sensitivity = Mathf.Clamp(sensitivity, minSensitivity, maxSensitivity);

        // onValueChanged non scatta se il valore salvato coincide con quello di default
        // dello slider, quindi applichiamo comunque i valori a mano (le Set* sono idempotenti).
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(volume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(sensitivity);

        SetVolume(volume);
        SetSensitivity(sensitivity);

        PlayerPrefs.Save();
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(PrefVolume, value);

        if (volumeValueLabel != null)
            volumeValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void SetSensitivity(float value)
    {
        if (player != null)
            player.mouseSensitivity = value;
        PlayerPrefs.SetFloat(PrefSensitivity, value);

        if (sensitivityValueLabel != null)
            sensitivityValueLabel.text = value.ToString("0.00");
    }

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (menuRoot != null)
            menuRoot.SetActive(open);

        // Riaprendo il menu si riparte sempre dalla schermata principale.
        ShowOptions(false);

        if (open)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (player != null) player.gameplayFrozen = true;
        }
        else
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (player != null) player.gameplayFrozen = false;
        }
    }

    // ----------------------------------------------------------------------
    //  Costruzione UI
    // ----------------------------------------------------------------------
    private void BuildUI()
    {
        // Canvas overlay in cima a tutto
        var canvasGO = new GameObject("PauseMenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Pannello di sfondo scuro semitrasparente a schermo intero (blocca i click dietro)
        menuRoot = new GameObject("PauseRoot", typeof(Image));
        menuRoot.transform.SetParent(canvasGO.transform, false);
        var bg = menuRoot.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);
        var bgRT = bg.rectTransform;
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // --- Pannello principale ---------------------------------------
        mainPanel = CreateFullscreenPanel(menuRoot.transform, "MainPanel");

        CreateLabel(mainPanel.transform, "Title", "MENU", menuFont, 150f,
            new Vector2(0f, 260f), new Vector2(900f, 260f), Color.white);

        CreateButton(mainPanel.transform, "ResumeButton", "Riprendi",
            new Vector2(0f, 40f), Resume);
        CreateButton(mainPanel.transform, "OptionsButton", "Opzioni",
            new Vector2(0f, -110f), () => ShowOptions(true));
        CreateButton(mainPanel.transform, "QuitButton", "Esci dal gioco",
            new Vector2(0f, -260f), QuitGame);

        // --- Sottomenu opzioni -------------------------------------------
        optionsPanel = CreateFullscreenPanel(menuRoot.transform, "OptionsPanel");

        CreateLabel(optionsPanel.transform, "Title", "OPZIONI", menuFont, 120f,
            new Vector2(0f, 280f), new Vector2(900f, 220f), Color.white);

        volumeSlider = CreateSliderRow(optionsPanel.transform, "VolumeRow", "Volume",
            80f, 0f, 1f, SetVolume, out volumeValueLabel);
        sensitivitySlider = CreateSliderRow(optionsPanel.transform, "SensitivityRow", "Sensibilità mouse",
            -40f, minSensitivity, maxSensitivity, SetSensitivity, out sensitivityValueLabel);

        CreateButton(optionsPanel.transform, "BackButton", "Indietro",
            new Vector2(0f, -240f), () => ShowOptions(false));
    }

    // Contenitore vuoto a schermo intero: serve solo a raggruppare i figli
    // così da poter accendere/spegnere un'intera schermata con SetActive.
    private GameObject CreateFullscreenPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
        TMP_FontAsset font, float fontSize, Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        if (font != null) label.font = font;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;

        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        return label;
    }

    private Button CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        if (buttonSprite != null)
        {
            img.sprite = buttonSprite;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        else
        {
            // Fallback se lo sprite non è assegnato
            img.color = new Color(0.85f, 0.8f, 0.7f, 1f);
        }

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520f, 130f);
        rt.anchoredPosition = anchoredPos;

        var button = go.GetComponent<Button>();
        button.targetGraphic = img;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.85f, 1f);
        colors.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        // Testo del bottone (figlio, riempie il bottone)
        var textLabel = CreateLabel(go.transform, "Label", label, buttonFont, 48f,
            Vector2.zero, rt.sizeDelta, new Color(0.12f, 0.08f, 0.05f, 1f));
        var textRT = textLabel.rectTransform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return button;
    }

    // Una riga del menu opzioni: etichetta a sinistra, slider al centro, valore a destra.
    private Slider CreateSliderRow(Transform parent, string name, string label, float y,
        float minValue, float maxValue, UnityEngine.Events.UnityAction<float> onChanged,
        out TextMeshProUGUI valueLabel)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0.5f, 0.5f);
        rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.pivot = new Vector2(0.5f, 0.5f);
        rowRT.sizeDelta = new Vector2(1120f, 80f);
        rowRT.anchoredPosition = new Vector2(0f, y);

        var nameLabel = CreateLabel(row.transform, "Label", label, buttonFont, 40f,
            new Vector2(-400f, 0f), new Vector2(320f, 60f), Color.white);
        nameLabel.alignment = TextAlignmentOptions.Left;

        valueLabel = CreateLabel(row.transform, "Value", "", buttonFont, 40f,
            new Vector2(470f, 0f), new Vector2(160f, 60f), Color.white);
        valueLabel.alignment = TextAlignmentOptions.Right;

        var slider = CreateSlider(row.transform, "Slider",
            new Vector2(70f, 0f), new Vector2(560f, 36f), minValue, maxValue);
        slider.onValueChanged.AddListener(onChanged);

        return slider;
    }

    // Slider costruito a mano: Unity richiede una gerarchia precisa
    // (Background, Fill Area/Fill, Handle Slide Area/Handle) perché fillRect e
    // handleRect vengono riposizionati dallo Slider stesso in base al valore.
    private Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos, Vector2 size,
        float minValue, float maxValue)
    {
        const float handleWidth = 36f;

        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        // Barra di fondo (traccia vuota)
        var background = new GameObject("Background", typeof(Image));
        background.transform.SetParent(go.transform, false);
        var bgImg = background.GetComponent<Image>();
        bgImg.color = new Color(0.15f, 0.12f, 0.1f, 0.9f);
        var bgRT = bgImg.rectTransform;
        bgRT.anchorMin = new Vector2(0f, 0.25f);
        bgRT.anchorMax = new Vector2(1f, 0.75f);
        bgRT.sizeDelta = Vector2.zero;

        // Area di riempimento (rientrata di mezza maniglia per lato)
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRT = fillArea.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRT.anchoredPosition = new Vector2(-handleWidth * 0.5f, 0f);
        fillAreaRT.sizeDelta = new Vector2(-handleWidth, 0f);

        var fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = new Color(0.85f, 0.8f, 0.7f, 1f);
        var fillRT = fillImg.rectTransform;
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.sizeDelta = new Vector2(handleWidth, 0f);

        // Area di scorrimento della maniglia
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaRT = handleArea.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.sizeDelta = new Vector2(-handleWidth, 0f);

        var handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var handleImg = handle.GetComponent<Image>();
        if (buttonSprite != null)
        {
            handleImg.sprite = buttonSprite;
            handleImg.type = Image.Type.Simple;
            handleImg.color = Color.white;
        }
        else
        {
            handleImg.color = Color.white;
        }
        var handleRT = handleImg.rectTransform;
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = new Vector2(0f, 1f);
        handleRT.sizeDelta = new Vector2(handleWidth, 0f);

        var slider = go.GetComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = false;

        var colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.85f, 1f);
        colors.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        slider.colors = colors;

        return slider;
    }

    // Garantisce un EventSystem in scena (necessario per i click UI col nuovo Input System)
    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.transform.SetParent(transform, false);
    }

#if UNITY_EDITOR
    // Auto-popola i riferimenti quando il componente viene aggiunto in editor,
    // così non serve trascinare nulla a mano nell'Inspector.
    private void Reset()
    {
        if (menuFont == null)
            menuFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Fonts/Brown Cookies SDF.asset");
        if (buttonFont == null)
            buttonFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Fonts/Huge Smile SDF.asset");
        if (buttonSprite == null)
            buttonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/FBX/UI_textures/lowpolypixelpaper.png");
    }
#endif
}
