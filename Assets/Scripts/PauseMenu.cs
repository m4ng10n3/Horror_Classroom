using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

// Menu di pausa costruito interamente da codice.
// - Si apre/chiude con ESC.
// - Da aperto ferma il tempo (Time.timeScale = 0) e mostra il cursore.
// - Contiene la scritta "MENU" e due bottoni: "Riprendi" ed "Esci dal gioco".
//
// Basta aggiungere questo componente a un GameObject vuoto in scena.
// I riferimenti a font e sprite si auto-popolano in editor (vedi Reset()).
[DisallowMultipleComponent]
public class PauseMenu : MonoBehaviour
{
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

    private GameObject menuRoot;   // pannello a schermo intero, attivato/disattivato
    private bool isOpen;
    private float previousTimeScale = 1f;

    void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<FPSController>();

        EnsureEventSystem();
        BuildUI();
        SetOpen(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
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

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (menuRoot != null)
            menuRoot.SetActive(open);

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

        // Scritta centrale "MENU"
        CreateLabel(menuRoot.transform, "Title", "MENU", menuFont, 150f,
            new Vector2(0f, 200f), new Vector2(900f, 260f), Color.white);

        // Bottoni
        CreateButton(menuRoot.transform, "ResumeButton", "Riprendi",
            new Vector2(0f, -20f), Resume);
        CreateButton(menuRoot.transform, "QuitButton", "Esci dal gioco",
            new Vector2(0f, -170f), QuitGame);
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
