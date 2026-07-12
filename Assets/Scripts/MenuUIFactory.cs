using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

// Costruzione da codice dei pezzi di UI condivisi dai menu (PauseMenu, MainMenu).
// Classe statica senza stato: font e sprite arrivano come parametri, così i due menu
// producono bottoni e slider identici e lo stile si cambia in un punto solo.
public static class MenuUIFactory
{
    // Colore del testo sui bottoni (scuro, per leggere sopra la carta chiara).
    public static readonly Color ButtonTextColor = new Color(0.12f, 0.08f, 0.05f, 1f);

    private static readonly Color BtnNormal = Color.white;
    private static readonly Color BtnHighlighted = new Color(1f, 0.95f, 0.85f, 1f);
    private static readonly Color BtnPressed = new Color(0.8f, 0.75f, 0.65f, 1f);
    private const float BtnFadeDuration = 0.08f;

    // Canvas overlay a schermo intero, scalato su una reference di 1920x1080.
    public static GameObject CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return go;
    }

    // Immagine a schermo intero. Con uno sprite assegnato viene ritagliata invece che
    // deformata (AspectRatioFitter in EnvelopeParent); senza sprite resta una tinta piatta.
    public static Image CreateFullscreenImage(Transform parent, string name, Sprite sprite,
        Color color, bool raycastTarget)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycastTarget;
        img.preserveAspect = false;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (sprite != null && sprite.rect.height > 0f)
        {
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        return img;
    }

    // Contenitore vuoto a schermo intero: serve solo a raggruppare i figli
    // così da poter accendere/spegnere un'intera schermata con SetActive.
    public static GameObject CreateFullscreenPanel(Transform parent, string name)
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

    public static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
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

    public static Button CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Sprite buttonSprite, TMP_FontAsset buttonFont,
        UnityEngine.Events.UnityAction onClick)
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
        colors.normalColor = BtnNormal;
        colors.highlightedColor = BtnHighlighted;
        colors.pressedColor = BtnPressed;
        colors.selectedColor = BtnNormal;
        colors.fadeDuration = BtnFadeDuration;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        // Testo del bottone (figlio, riempie il bottone)
        var textLabel = CreateLabel(go.transform, "Label", label, buttonFont, 48f,
            Vector2.zero, rt.sizeDelta, ButtonTextColor);
        var textRT = textLabel.rectTransform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return button;
    }

    // Una riga del menu opzioni: etichetta a sinistra, slider al centro, valore a destra.
    public static Slider CreateSliderRow(Transform parent, string name, string label, float y,
        float minValue, float maxValue, Sprite handleSprite, TMP_FontAsset labelFont,
        UnityEngine.Events.UnityAction<float> onChanged, out TextMeshProUGUI valueLabel)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0.5f, 0.5f);
        rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.pivot = new Vector2(0.5f, 0.5f);
        rowRT.sizeDelta = new Vector2(1120f, 80f);
        rowRT.anchoredPosition = new Vector2(0f, y);

        var nameLabel = CreateLabel(row.transform, "Label", label, labelFont, 40f,
            new Vector2(-400f, 0f), new Vector2(320f, 60f), Color.white);
        nameLabel.alignment = TextAlignmentOptions.Left;

        valueLabel = CreateLabel(row.transform, "Value", "", labelFont, 40f,
            new Vector2(470f, 0f), new Vector2(160f, 60f), Color.white);
        valueLabel.alignment = TextAlignmentOptions.Right;

        var slider = CreateSlider(row.transform, "Slider",
            new Vector2(70f, 0f), new Vector2(560f, 36f), minValue, maxValue, handleSprite);
        slider.onValueChanged.AddListener(onChanged);

        return slider;
    }

    // Slider costruito a mano: Unity richiede una gerarchia precisa
    // (Background, Fill Area/Fill, Handle Slide Area/Handle) perché fillRect e
    // handleRect vengono riposizionati dallo Slider stesso in base al valore.
    public static Slider CreateSlider(Transform parent, string name, Vector2 anchoredPos,
        Vector2 size, float minValue, float maxValue, Sprite handleSprite)
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
        if (handleSprite != null)
        {
            handleImg.sprite = handleSprite;
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
        colors.normalColor = BtnNormal;
        colors.highlightedColor = BtnHighlighted;
        colors.pressedColor = BtnPressed;
        colors.selectedColor = BtnNormal;
        colors.fadeDuration = BtnFadeDuration;
        slider.colors = colors;

        return slider;
    }

    // Garantisce un EventSystem in scena (necessario per i click UI col nuovo Input System)
    public static void EnsureEventSystem(Transform parent)
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.transform.SetParent(parent, false);
    }
}
