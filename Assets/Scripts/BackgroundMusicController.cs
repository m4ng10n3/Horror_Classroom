using UnityEngine;

// Musica di sottofondo del livello. Suona in loop a velocità "base" e, quando il
// giocatore sta rubando (hold di StealableItem in corso), accelera il pitch fino a
// stealPitch. Appena il furto si ferma (rilascio, completamento o annullamento) la
// musica torna gradualmente alla velocità base. La transizione è morbida (lerp), non
// uno scatto. Singleton come PhysicalInventory così gli oggetti la trovano facilmente.
[RequireComponent(typeof(AudioSource))]
public class BackgroundMusicController : MonoBehaviour
{
    public static BackgroundMusicController Instance { get; private set; }

    [Header("Musica")]
    [Tooltip("Traccia da suonare in loop. Se l'AudioSource ha già una clip, questa è opzionale.")]
    public AudioClip musicClip;

    [Range(0f, 1f)]
    [Tooltip("Volume della musica di sottofondo.")]
    public float volume = 0.5f;

    [Header("Pitch")]
    [Min(0.1f)]
    [Tooltip("Velocità/pitch normale quando non stai rubando.")]
    public float basePitch = 1f;

    [Min(0.1f)]
    [Tooltip("Velocità/pitch raggiunto mentre il furto è in corso. >1 = più veloce.")]
    public float stealPitch = 1.5f;

    [Min(0.01f)]
    [Tooltip("Quanto rapidamente il pitch si avvicina al valore obiettivo (più alto = transizione più veloce).")]
    public float pitchLerpSpeed = 4f;

    private AudioSource source;
    private bool stealing;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        source = GetComponent<AudioSource>();
        if (musicClip != null)
            source.clip = musicClip;

        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;
        source.pitch = basePitch;
    }

    void Start()
    {
        if (source.clip != null && !source.isPlaying)
            source.Play();
    }

    void Update()
    {
        float targetPitch = stealing ? stealPitch : basePitch;
        source.pitch = Mathf.Lerp(source.pitch, targetPitch, pitchLerpSpeed * Time.deltaTime);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Chiamato ogni frame mentre un furto è in corso: mantiene la musica accelerata.
    public void SetStealing(bool active)
    {
        stealing = active;
    }
}
