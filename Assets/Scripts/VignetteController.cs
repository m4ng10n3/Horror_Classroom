using UnityEngine;
using UnityEngine.UI;

public class VignetteController : MonoBehaviour
{
    [Header("References")]
    public Image vignetteImage;
    public TeacherStateMachine teacherStateMachine;
    public SuspicionCounter suspicionCounter;

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

    void Start()
    {
        if (vignetteImage != null)
        {
            // Attiva il GameObject della vignetta se era disattivato nell'Inspector
            if (!vignetteImage.gameObject.activeSelf)
                vignetteImage.gameObject.SetActive(true);

            currentColor = neutralColor;
            currentAlpha = 0f;
            ApplyVisuals();
        }
    }

    void Update()
    {
        CalculateTargets();

        // Transizione fluida verso il target
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * transitionSpeed);
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * transitionSpeed);

        // Pulsazione (usata in Fase 5 durante la finestra esplorazione)
        if (isPulsing)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f;
            currentAlpha += pulse * pulseIntensity;
        }

        ApplyVisuals();
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

    private void ApplyVisuals()
    {
        if (vignetteImage == null) return;

        Color c = targetColor;
        c.a = Mathf.Clamp01(currentAlpha);
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
        currentAlpha = alpha;
    }
}