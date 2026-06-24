using UnityEngine;
using UnityEngine.UI;

public class VignetteController : MonoBehaviour
{
    public static VignetteController Instance { get; private set; }

    [Header("References")]
    public Image vignetteImage;
    public TeacherStateMachine teacherStateMachine;
    public SuspicionCounter suspicionCounter;

    [Header("Steal Vignette")]
    [Tooltip("Colore della vignettatura mostrata mentre rubi.")]
    public Color stealColor = new Color(0f, 0f, 0f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Alpha massimo della vignettatura di furto, raggiunto a furto quasi completo.")]
    public float stealMaxAlpha = 0.6f;
    [Tooltip("Velocità di dissolvenza in entrata/uscita della vignettatura di furto.")]
    public float stealFadeSpeed = 6f;

    [Header("Steal Vignette - Forma")]
    [Tooltip("Se vignetteImage non ha uno sprite, ne genera uno a runtime: centro trasparente, bordi neri.")]
    public bool autoGenerateSprite = true;
    [Range(0f, 1f)]
    [Tooltip("Frazione del raggio che resta completamente trasparente al centro. Più alto = bordo nero più sottile.")]
    public float vignetteInnerRadius = 0.55f;
    [Range(0.01f, 1f)]
    [Tooltip("Morbidezza della transizione dal centro trasparente al bordo nero.")]
    public float vignetteSoftness = 0.45f;

    private bool isStealing = false;
    private float stealProgress = 0f;
    private float stealAlpha = 0f;

    [Header("Base Intensity per State")]
    [Range(0f, 1f)] public float neutralAlpha = 0f;
    [Range(0f, 1f)] public float pleasedAlpha = 0.15f;
    [Range(0f, 1f)] public float angryAlpha = 0.4f;

    [Header("Suspicion Bonus")]
    [Tooltip("Alpha aggiuntivo per ogni punto di sospetto")]
    public float suspicionAlphaPerPoint = 0.05f;

    [Header("Transition")]
    public float transitionSpeed = 2f;

    [Header("Colors")]
    public Color neutralColor = new Color(0f, 0f, 0f, 1f);
    public Color pleasedColor = new Color(0.3f, 0.15f, 0f, 1f);
    public Color angryColor = new Color(0.5f, 0f, 0f, 1f);

    private float targetAlpha = 0f;
    private Color targetColor;
    private float currentAlpha = 0f;
    private Color currentColor;

    // Per la pulsazione durante la finestra esplorazione (Fase 5)
    private bool isPulsing = false;
    private float pulseSpeed = 3f;
    private float pulseIntensity = 0.3f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (vignetteImage != null)
        {
            // Bordi neri, centro trasparente: se non c'è già uno sprite, lo generiamo.
            if (autoGenerateSprite && vignetteImage.sprite == null)
            {
                vignetteImage.sprite = GenerateVignetteSprite();
                vignetteImage.type = Image.Type.Simple;
            }

            vignetteImage.gameObject.SetActive(false);
        }
    }

    // Crea uno sprite di vignettatura radiale: alpha 0 al centro, 1 ai bordi (smoothstep).
    // L'RGB è bianco così il tint nero di stealColor lo rende nero solo dove è opaco.
    private Sprite GenerateVignetteSprite(int size = 256)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;
        float innerEnd = vignetteInnerRadius;
        float outerEnd = vignetteInnerRadius + vignetteSoftness;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
                float a = Mathf.Clamp01(Mathf.InverseLerp(innerEnd, outerEnd, d));
                a = a * a * (3f - 2f * a); // smoothstep
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        UpdateStealVignette();
    }

    /// <summary>
    /// Attiva/disattiva la vignettatura mostrata durante il furto. L'intensità cresce
    /// con il progresso del furto (0..1). Sistema isolato: non tocca la logica base.
    /// </summary>
    public void SetStealing(bool active, float progress01 = 1f)
    {
        isStealing = active;
        stealProgress = Mathf.Clamp01(progress01);
    }

    private void UpdateStealVignette()
    {
        if (vignetteImage == null) return;

        // A furto in corso punta a un alpha proporzionale al progresso; altrimenti svanisce.
        float target = isStealing ? stealMaxAlpha * stealProgress : 0f;
        stealAlpha = Mathf.MoveTowards(stealAlpha, target, stealFadeSpeed * Time.deltaTime);

        bool shouldShow = stealAlpha > 0.001f;
        if (vignetteImage.gameObject.activeSelf != shouldShow)
            vignetteImage.gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            Color c = stealColor;
            c.a = stealAlpha;
            vignetteImage.color = c;
        }
    }

    private void CalculateTargets()
    {
        // Base: stato della prof
        float baseAlpha = neutralAlpha;
        targetColor = neutralColor;

        if (teacherStateMachine != null)
        {
            switch (teacherStateMachine.CurrentState)
            {
                case TeacherState.Neutral:
                    baseAlpha = neutralAlpha;
                    targetColor = neutralColor;
                    break;
                case TeacherState.Pleased:
                    baseAlpha = pleasedAlpha;
                    targetColor = pleasedColor;
                    break;
                case TeacherState.Angry:
                    baseAlpha = angryAlpha;
                    targetColor = angryColor;
                    break;
            }
        }

        // Bonus dal sospetto
        float suspicionBonus = 0f;
        if (suspicionCounter != null)
        {
            suspicionBonus = suspicionCounter.CurrentSuspicion * suspicionAlphaPerPoint;
        }

        targetAlpha = Mathf.Clamp01(baseAlpha + suspicionBonus);
    }

    private void ApplyVisuals(float alpha)
    {
        if (vignetteImage == null) return;

        Color c = currentColor;
        c.a = alpha;
        vignetteImage.color = c;
    }

    /// <summary>
    /// Attiva la pulsazione (Fase 5: prof sta per girarsi)
    /// </summary>
    public void StartPulsing(float speed = 3f, float intensity = 0.3f)
    {
        isPulsing = true;
        pulseSpeed = speed;
        pulseIntensity = intensity;
    }

    /// <summary>
    /// Ferma la pulsazione
    /// </summary>
    public void StopPulsing()
    {
        isPulsing = false;
    }

    /// <summary>
    /// Flash immediato (per jumpscare o eventi shock)
    /// </summary>
    public void Flash(float alpha = 0.8f)
    {
        currentAlpha = Mathf.Min(alpha, 0.6f);
    }
}