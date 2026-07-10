using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class TeacherChalkAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Il gessetto che si muove da solo sulla lavagna (es. istanza del prefab Gesso_1)")]
    public Transform chalk;
    [Tooltip("Punto di partenza della scarabocchiata, a sinistra della lavagna")]
    public Transform startPoint;
    [Tooltip("Punto di arrivo della scarabocchiata, a destra della lavagna")]
    public Transform endPoint;
    [Tooltip("Contenitore dei tratti generati a runtime. Il suo asse Z deve puntare fuori dalla lavagna (normale della superficie). Se vuoto usa questo stesso transform")]
    public Transform strokesContainer;

    [Header("Aspetto del segno")]
    public Material chalkLineMaterial;
    public float lineWidth = 0.02f;

    [Header("Movimento")]
    [Tooltip("Ampiezza dell'oscillazione verticale del gessetto mentre scarabocchia")]
    public float verticalJitter = 0.05f;
    [Tooltip("Velocità dell'oscillazione verticale")]
    public float jitterFrequency = 6f;
    [Tooltip("Velocità di rotazione 'a scatti' del gessetto (effetto inquietudine)")]
    public float wobbleSpeed = 240f;
    [Tooltip("Quanti punti aggiungere al secondo lungo un tratto")]
    public int pointsPerSecond = 12;

    [Header("Tratteggio (il gesso si alza e si abbassa)")]
    public float minStrokeDuration = 0.15f;
    public float maxStrokeDuration = 0.5f;
    public float minGapDuration = 0.05f;
    public float maxGapDuration = 0.2f;

    [Tooltip("Se vero, i tratti precedenti vengono eliminati ad ogni nuova finestra di esplorazione")]
    public bool clearOnStart = true;

    [Header("Audio")]
    [Tooltip("AudioSource posizionata sul gessetto/lavagna (consigliato Spatial Blend 3D)")]
    public AudioSource chalkAudioSource;
    [Tooltip("Una viene scelta a caso ad ogni finestra di esplorazione")]
    public AudioClip[] chalkWriteClips;
    [Tooltip("Velocità del fade tra tratto e pausa (volume/secondo). Evita il click che si sente con Pause/UnPause secchi")]
    public float audioFadeSpeed = 10f;

    [Header("Colore del gesso")]
    [Tooltip("Colore (albedo) dei tratti di gesso.")]
    [SerializeField] private Color chalkColor = Color.white;

    [Tooltip("Emissione dei tratti: se diversa da nero il gesso si auto-illumina, restando ben " +
             "visibile anche al buio (es. gesso rosso 'elettrico'). HDR: alza l'intensità per un effetto più acceso.")]
    [ColorUsage(true, true)] [SerializeField] private Color chalkEmission = Color.black;

    public Color ChalkColor => chalkColor;
    public Color ChalkEmission => chalkEmission;

    public void SetChalkColor(Color color, Color emission)
    {
        chalkColor = color;
        chalkEmission = emission;
    }

    private Coroutine activeRoutine;
    private float baseChalkVolume = 1f;

    void Awake()
    {
        if (chalkAudioSource != null)
            baseChalkVolume = chalkAudioSource.volume;
    }

    public void PlayScribble(float duration)
    {
        if (startPoint == null || endPoint == null) return;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(ScribbleRoutine(Mathf.Max(0.01f, duration)));
    }

    public void StopScribble()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (chalk != null)
            chalk.gameObject.SetActive(false);

        StopChalkAudio();
    }

    IEnumerator ScribbleRoutine(float duration)
    {
        if (clearOnStart)
            ClearStrokes();

        if (chalk != null)
        {
            chalk.gameObject.SetActive(true);
            chalk.position = startPoint.position;
        }

        float targetVolume = 0f;
        if (chalkAudioSource != null && chalkWriteClips != null && chalkWriteClips.Length > 0)
        {
            chalkAudioSource.clip = chalkWriteClips[Random.Range(0, chalkWriteClips.Length)];
            chalkAudioSource.loop = true;
            chalkAudioSource.volume = 0f;
            chalkAudioSource.Play();
            targetVolume = baseChalkVolume;
        }

        float elapsed = 0f;
        float pointTimer = 0f;
        float pointInterval = 1f / Mathf.Max(1f, pointsPerSecond);

        bool penDown = true;
        float stateTimer = Random.Range(minStrokeDuration, maxStrokeDuration);
        LineRenderer currentStroke = CreateNewStroke();
        AddScribblePoint(currentStroke, startPoint.position);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            pointTimer += Time.deltaTime;
            stateTimer -= Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 basePos = Vector3.Lerp(startPoint.position, endPoint.position, t);
            float jitter = Mathf.Sin(elapsed * jitterFrequency * Mathf.PI * 2f) * verticalJitter;
            Vector3 tipPos = basePos + Vector3.up * jitter;

            if (chalk != null)
            {
                chalk.position = tipPos;
                chalk.Rotate(Vector3.forward, wobbleSpeed * Time.deltaTime, Space.Self);
            }

            if (stateTimer <= 0f)
            {
                penDown = !penDown;
                pointTimer = 0f;
                if (penDown)
                {
                    currentStroke = CreateNewStroke();
                    AddScribblePoint(currentStroke, tipPos);
                    stateTimer = Random.Range(minStrokeDuration, maxStrokeDuration);
                    targetVolume = baseChalkVolume;
                }
                else
                {
                    currentStroke = null;
                    stateTimer = Random.Range(minGapDuration, maxGapDuration);
                    targetVolume = 0f;
                }
            }
            else if (penDown && pointTimer >= pointInterval)
            {
                pointTimer = 0f;
                AddScribblePoint(currentStroke, tipPos);
            }

            if (chalkAudioSource != null)
                chalkAudioSource.volume = Mathf.MoveTowards(chalkAudioSource.volume, targetVolume, audioFadeSpeed * Time.deltaTime);

            yield return null;
        }

        if (penDown)
            AddScribblePoint(currentStroke, endPoint.position);

        if (chalk != null)
            chalk.gameObject.SetActive(false);

        StopChalkAudio();

        activeRoutine = null;
    }

    void StopChalkAudio()
    {
        if (chalkAudioSource != null)
        {
            chalkAudioSource.Stop();
            chalkAudioSource.volume = baseChalkVolume;
        }
    }

    LineRenderer CreateNewStroke()
    {
        Transform parent = strokesContainer != null ? strokesContainer : transform;

        GameObject go = new GameObject("ChalkStroke");
        go.transform.SetParent(parent, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.widthMultiplier = lineWidth;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (chalkLineMaterial != null)
        {
            line.sharedMaterial = chalkLineMaterial;
            // Il materiale del gesso usa URP/Lit, che ignora i vertex color (startColor/endColor):
            // il colore va imposto sul materiale. line.material istanzia una copia di proprietà del
            // renderer (distrutta con lo stroke, niente leak) senza toccare l'asset condiviso.
            Material mat = line.material;
            mat.color = chalkColor;            // _BaseColor (albedo), la [MainColor] di URP/Lit
            ApplyEmission(mat, chalkEmission); // auto-illuminazione: gesso ben visibile al buio
        }
        line.positionCount = 0;

        return line;
    }

    // Imposta (o azzera) l'emissione del materiale del gesso. Con emissione != nero il tratto si
    // auto-illumina e resta visibile anche al buio, indipendente dalla luce di scena (URP/Lit).
    void ApplyEmission(Material mat, Color emission)
    {
        if (emission.maxColorComponent > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
    }

    void AddScribblePoint(LineRenderer line, Vector3 worldPos)
    {
        if (line == null) return;
        int count = line.positionCount;
        line.positionCount = count + 1;
        line.SetPosition(count, worldPos);
    }

    void ClearStrokes()
    {
        Transform parent = strokesContainer != null ? strokesContainer : transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }
}
