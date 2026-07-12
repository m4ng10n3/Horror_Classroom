using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

// Menu iniziale, costruito da codice con gli stessi pezzi di UI del PauseMenu
// (vedi MenuUIFactory): immagine di sfondo, titolo e tre bottoni sopra.
// - "Gioca" carica la scena di gioco.
// - "Opzioni" apre lo stesso sottomenu volume/sensibilita' della pausa: i valori
//   finiscono in PlayerPrefs e vengono riapplicati al player quando la partita parte.
// - "Esci dal gioco" chiude l'applicazione.
//
// Va messo su un GameObject vuoto nella scena del menu.
// Font e sprite dei bottoni si auto-popolano in editor (vedi Reset()).
[DisallowMultipleComponent]
public class MainMenu : MonoBehaviour
{
    // Stesse chiavi del PauseMenu: le impostazioni scelte qui valgono anche in partita.
    private const string PrefVolume = "Options_Volume";
    private const string PrefSensitivity = "Options_MouseSensitivity";

    [Header("Scena di gioco")]
    [Tooltip("Nome della scena caricata da \"Gioca\". Deve essere nella lista del Build Settings.")]
    public string gameSceneName = "Classe";

    [Header("Titolo")]
    public string title = "HORROR CLASSROOM";

    [Header("Font & Sprite (auto-caricati in editor)")]
    [Tooltip("Font del titolo (Brown Cookies SDF)")]
    public TMP_FontAsset menuFont;
    [Tooltip("Font dei bottoni (Huge Smile SDF)")]
    public TMP_FontAsset buttonFont;
    [Tooltip("Sprite di sfondo dei bottoni (lowpolypixelpaper)")]
    public Sprite buttonSprite;

    [Header("Sfondo")]
    [Tooltip("Immagine a schermo intero dietro ai bottoni. Va importata come Sprite (2D and UI). Se vuota resta uno sfondo scuro.")]
    public Sprite backgroundImage;
    [Tooltip("Quanto scurire l'immagine, per far risaltare i bottoni. 0 = immagine pulita, 1 = nero pieno.")]
    [Range(0f, 1f)] public float backgroundDim = 0.45f;

    [Header("Opzioni - limiti degli slider")]
    [Tooltip("Sensibilita' mouse minima (estremo sinistro dello slider). Deve combaciare con PauseMenu.")]
    public float minSensitivity = 0.02f;
    [Tooltip("Sensibilita' mouse massima (estremo destro dello slider). Deve combaciare con PauseMenu.")]
    public float maxSensitivity = 0.5f;
    [Tooltip("Valore usato al primo avvio, quando non c'e' ancora nulla in PlayerPrefs. Tienilo uguale a FPSController.mouseSensitivity.")]
    public float defaultSensitivity = 0.1f;

    private GameObject mainPanel;     // titolo + i tre bottoni
    private GameObject optionsPanel;  // sottomenu OPZIONI con i due slider
    private Slider volumeSlider;
    private Slider sensitivitySlider;
    private TextMeshProUGUI volumeValueLabel;
    private TextMeshProUGUI sensitivityValueLabel;
    private bool optionsOpen;

    void Start()
    {
        // Tornando qui da una partita in pausa il tempo sarebbe ancora fermo.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        MenuUIFactory.EnsureEventSystem(transform);
        BuildUI();
        LoadSettings();
        ShowOptions(false);
    }

    void Update()
    {
        // Da dentro le opzioni, ESC torna al menu principale.
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && optionsOpen)
            ShowOptions(false);
    }

    // Chiamato dal bottone "Gioca".
    public void PlayGame()
    {
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    // Chiamato dal bottone "Esci dal gioco".
    public void QuitGame()
    {
        PlayerPrefs.Save();
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
        // Senza chiave salvata si parte dal default del player: scriverne uno diverso
        // qui significherebbe cambiare la sensibilita' della partita senza chiederlo.
        float sensitivity = PlayerPrefs.GetFloat(PrefSensitivity, defaultSensitivity);
        sensitivity = Mathf.Clamp(sensitivity, minSensitivity, maxSensitivity);

        // onValueChanged non scatta se il valore salvato coincide con quello di default
        // dello slider, quindi applichiamo comunque i valori a mano.
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(volume);
        if (sensitivitySlider != null) sensitivitySlider.SetValueWithoutNotify(sensitivity);

        SetVolume(volume);
        SetSensitivity(sensitivity);

        PlayerPrefs.Save();
    }

    private void SetVolume(float value)
    {
        // AudioListener.volume e' globale e sopravvive al cambio scena.
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(PrefVolume, value);

        if (volumeValueLabel != null)
            volumeValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void SetSensitivity(float value)
    {
        // Qui il player non esiste: il valore viaggia via PlayerPrefs e viene applicato
        // da PauseMenu.LoadSettings() quando la scena di gioco parte.
        PlayerPrefs.SetFloat(PrefSensitivity, value);

        if (sensitivityValueLabel != null)
            sensitivityValueLabel.text = value.ToString("0.00");
    }

    // ----------------------------------------------------------------------
    //  Costruzione UI
    // ----------------------------------------------------------------------
    private void BuildUI()
    {
        var canvasGO = MenuUIFactory.CreateCanvas(transform, "MainMenuCanvas", 0);

        // L'ordine dei figli e' l'ordine di disegno: prima lo sfondo, poi la velina,
        // poi i pannelli. I primi due non sono raycast target, altrimenti mangerebbero i click.
        MenuUIFactory.CreateFullscreenImage(canvasGO.transform, "Background",
            backgroundImage,
            backgroundImage != null ? Color.white : new Color(0.06f, 0.05f, 0.07f, 1f),
            false);

        MenuUIFactory.CreateFullscreenImage(canvasGO.transform, "Dim",
            null, new Color(0f, 0f, 0f, backgroundDim), false);

        // --- Pannello principale ---------------------------------------
        mainPanel = MenuUIFactory.CreateFullscreenPanel(canvasGO.transform, "MainPanel");

        MenuUIFactory.CreateLabel(mainPanel.transform, "Title", title, menuFont, 130f,
            new Vector2(0f, 280f), new Vector2(1400f, 300f), Color.white);

        MenuUIFactory.CreateButton(mainPanel.transform, "PlayButton", "Gioca",
            new Vector2(0f, 40f), buttonSprite, buttonFont, PlayGame);
        MenuUIFactory.CreateButton(mainPanel.transform, "OptionsButton", "Opzioni",
            new Vector2(0f, -110f), buttonSprite, buttonFont, () => ShowOptions(true));
        MenuUIFactory.CreateButton(mainPanel.transform, "QuitButton", "Esci dal gioco",
            new Vector2(0f, -260f), buttonSprite, buttonFont, QuitGame);

        // --- Sottomenu opzioni -------------------------------------------
        optionsPanel = MenuUIFactory.CreateFullscreenPanel(canvasGO.transform, "OptionsPanel");

        MenuUIFactory.CreateLabel(optionsPanel.transform, "Title", "OPZIONI", menuFont, 120f,
            new Vector2(0f, 280f), new Vector2(900f, 220f), Color.white);

        volumeSlider = MenuUIFactory.CreateSliderRow(optionsPanel.transform, "VolumeRow", "Volume",
            80f, 0f, 1f, buttonSprite, buttonFont, SetVolume, out volumeValueLabel);
        sensitivitySlider = MenuUIFactory.CreateSliderRow(optionsPanel.transform, "SensitivityRow",
            "Sensibilità mouse", -40f, minSensitivity, maxSensitivity, buttonSprite, buttonFont,
            SetSensitivity, out sensitivityValueLabel);

        MenuUIFactory.CreateButton(optionsPanel.transform, "BackButton", "Indietro",
            new Vector2(0f, -240f), buttonSprite, buttonFont, () => ShowOptions(false));
    }

#if UNITY_EDITOR
    // Auto-popola i riferimenti quando il componente viene aggiunto in editor,
    // cosi' non serve trascinare nulla a mano nell'Inspector (tranne l'immagine di sfondo).
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
