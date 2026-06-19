using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Abbassa progressivamente il Render Scale dell'URP Asset attivo ogni volta che
/// il player si siede (evento FPSController.OnPlayerSatDown).
/// In Play parte da <see cref="startScale"/> e scende di <see cref="decrementPerSit"/>
/// ad ogni "sit down", fino a non andare mai sotto <see cref="minScale"/>.
/// Il valore originale dell'asset viene ripristinato all'uscita dal Play, così
/// l'asset su disco non resta "sporcato".
/// </summary>
public class RenderScaleDegrader : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Se vuoto, viene cercato automaticamente in scena.")]
    public FPSController player;

    [Header("Render Scale")]
    [Tooltip("Render scale all'avvio del Play.")]
    [Range(0.05f, 1f)] public float startScale = 0.8f;

    [Tooltip("Render scale minimo: non scende mai sotto questo valore.")]
    [Range(0.05f, 1f)] public float minScale = 0.33f;

    [Tooltip("Di quanto cala il render scale ad ogni volta che il player si siede.")]
    [Range(0.01f, 0.5f)] public float decrementPerSit = 0.05f;

    private UniversalRenderPipelineAsset urp;
    private float originalScale = 1f;
    private bool captured;
    private float currentScale;

    void Awake()
    {
        if (player == null) player = FindFirstObjectByType<FPSController>();
        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    }

    void OnEnable()
    {
        if (player != null) player.OnPlayerSatDown += HandleSatDown;
    }

    void OnDisable()
    {
        if (player != null) player.OnPlayerSatDown -= HandleSatDown;
        RestoreOriginal();
    }

    void Start()
    {
        if (urp == null)
        {
            Debug.LogWarning("[RenderScaleDegrader] Nessun UniversalRenderPipelineAsset attivo trovato.");
            enabled = false;
            return;
        }

        if (player == null)
            Debug.LogWarning("[RenderScaleDegrader] FPSController non trovato: il render scale partirà ma non calerà.");

        // Memorizza il valore originale dell'asset PRIMA di modificarlo
        if (!captured) { originalScale = urp.renderScale; captured = true; }

        currentScale = startScale;
        urp.renderScale = currentScale;
    }

    void HandleSatDown()
    {
        if (urp == null) return;
        currentScale = Mathf.Max(minScale, currentScale - decrementPerSit);
        urp.renderScale = currentScale;
        Debug.Log($"[RenderScaleDegrader] Sit → renderScale = {currentScale:0.###}");
    }

    /// <summary>Riporta il render scale al valore di partenza (utile per un restart manuale).</summary>
    public void ResetToStart()
    {
        if (urp == null) return;
        currentScale = startScale;
        urp.renderScale = currentScale;
    }

    private void RestoreOriginal()
    {
        if (urp != null && captured) urp.renderScale = originalScale;
    }
}
