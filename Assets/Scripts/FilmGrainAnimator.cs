using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class FilmGrainAnimator : MonoBehaviour
{
    [Header("CRT Grain Flicker")]
    [Range(0f, 1f)] public float intensityMin = 0.3f;
    [Range(0f, 1f)] public float intensityMax = 0.7f;

    [Tooltip("Cambia il response ogni X secondi (simula refresh rate CRT)")]
    public float flickerRate = 0.05f;

    private FilmGrain filmGrain;
    private float flickerTimer;
    private float targetIntensity;

    void Start()
    {
        if (!TryGetComponent(out Volume volume)) return;
        if (!volume.profile.TryGet(out filmGrain)) return;

        targetIntensity = Random.Range(intensityMin, intensityMax);
    }

    void Update()
    {
        if (filmGrain == null) return;

        flickerTimer += Time.deltaTime;
        if (flickerTimer >= flickerRate)
        {
            flickerTimer = 0f;
            targetIntensity = Random.Range(intensityMin, intensityMax);
            filmGrain.intensity.Override(targetIntensity);
        }
    }
}
