using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FireAudio : MonoBehaviour
{
    [Header("Clip")]
    [SerializeField] AudioClip _fireClip;

    [Header("Distances")]
    [Tooltip("Distanza entro cui il volume è massimo")]
    [SerializeField] float _minDistance = 1.5f;
    [Tooltip("Distanza oltre cui il fuoco è inudibile")]
    [SerializeField] float _maxDistance = 12f;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] float _volume = 1f;

    AudioSource _source;

    void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.clip = _fireClip;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 1f;                             // full 3D
        _source.rolloffMode = AudioRolloffMode.Logarithmic;  // caduta naturale
        _source.minDistance = _minDistance;
        _source.maxDistance = _maxDistance;
        _source.volume = _volume;
        _source.dopplerLevel = 0f;                            // il fuoco non si sposta
    }

    void Start()
    {
        if (_fireClip != null)
            _source.Play();
    }
}
