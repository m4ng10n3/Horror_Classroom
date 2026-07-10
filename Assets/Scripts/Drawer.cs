using System.Collections;
using UnityEngine;

public class Drawer : MonoBehaviour, IPlayerInteractable, ISecondaryInteractable
{
    [Header("Scorrimento")]
    [Tooltip("Posizione X LOCALE che il cassetto raggiunge da aperto. La Y e la Z restano quelle di partenza.")]
    public float openX = -19.707f;

    [Min(0.01f)]
    [Tooltip("Velocità di scorrimento in unità al secondo.")]
    public float speed = 2f;

    [Header("Oggetto contenuto (opzionale)")]
    [Tooltip("PickupItem dentro il cassetto. Se vuoto, viene cercato tra i figli. Lo raccogli mirando al CASSETTO aperto, non all'oggetto.")]
    public PickupItem pickupInside;

    [Header("Blocco con oggetto (opzionale)")]
    [Tooltip("Se valorizzato, il cassetto si apre solo se questo oggetto è nell'inventario fisico (es. \"Graffetta\"). Lascia vuoto per un cassetto sempre apribile.")]
    public string requiredItemName = "";

    [Tooltip("Se attivo, l'oggetto richiesto viene consumato (rimosso dall'inventario) la prima volta che apri il cassetto.")]
    public bool consumeItemOnOpen = false;

    [Header("Audio")]
    [Tooltip("Suono riprodotto quando il cassetto inizia ad aprirsi (es. opendrawer).")]
    public AudioClip openClip;

    [Tooltip("Suono riprodotto quando il cassetto inizia a chiudersi (es. closedrawer).")]
    public AudioClip closeClip;

    [Tooltip("AudioSource usata per i suoni. Se vuota viene presa/aggiunta su questo oggetto in automatico.")]
    public AudioSource audioSource;

    [Range(0f, 1f)]
    [Tooltip("Volume dei suoni di apertura/chiusura.")]
    public float soundVolume = 1f;

    [Range(0f, 1f)]
    [Tooltip("0 = suono 2D (uguale ovunque), 1 = suono 3D posizionale (si sente più forte vicino al cassetto).")]
    public float spatialBlend = 1f;

    [Header("Testi")]
    public string openPrompt = "[F] Apri cassetto";
    public string closePrompt = "[F] Chiudi cassetto";
    [Tooltip("Mostrato quando il cassetto è aperto con un oggetto dentro. Il tasto qui scritto deve combaciare con 'Secondary Interact Key' del PlayerInteractionController.")]
    public string secondaryClosePrompt = "[Q] Chiudi senza raccogliere";

    [Tooltip("Prompt mostrato quando il cassetto è bloccato e non hai l'oggetto richiesto.")]
    public string lockedPrompt = "[F] Cassetto bloccato";

    [TextArea(2, 3)]
    [Tooltip("Messaggio mostrato se provi ad aprire senza l'oggetto richiesto.")]
    public string lockedMessage = "Il cassetto è bloccato. Mi serve qualcosa per forzare la serratura.";

    private bool unlocked = false;
    private Vector3 closedPos;
    private Vector3 OpenPos => new Vector3(openX, closedPos.y, closedPos.z);

    private bool isOpen = false;
    private bool animating = false;
    private Coroutine slideRoutine;

    void Awake()
    {
        // La posizione "chiusa" è semplicemente dove il cassetto si trova in scena.
        closedPos = transform.localPosition;

        if (pickupInside == null)
        {
            pickupInside = GetComponentInChildren<PickupItem>(true);
        }

        // AudioSource: usa quella assegnata, altrimenti prendi/aggiungi quella sul cassetto.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = spatialBlend;
    }

    void Start()
    {
        // Spegne i collider dell'oggetto interno: si raccoglie mirando al cassetto,
        // quindi l'astuccio non deve né bloccare il giocatore né rubare la mira.
        if (pickupInside != null)
        {
            foreach (Collider col in pickupInside.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }
    }

    public bool CanInteract() => !animating && isActiveAndEnabled;

    public string GetInteractionPrompt()
    {
        if (!isOpen)
        {
            return IsLocked() ? lockedPrompt : openPrompt;
        }

        if (HasGrabbablePickup())
        {
            return pickupInside.GetInteractionPrompt();
        }

        return closePrompt;
    }

    public string Interact(GameManager gameManager)
    {
        if (animating)
        {
            return string.Empty;
        }

        // 1) Chiuso -> apri (se non è bloccato da un oggetto richiesto).
        if (!isOpen)
        {
            if (IsLocked())
            {
                return lockedMessage;
            }

            UnlockWithItem();
            StartSlide(OpenPos, true);
            return string.Empty;
        }

        // 2) Aperto con oggetto dentro -> raccoglilo (delega al PickupItem).
        if (HasGrabbablePickup())
        {
            return pickupInside.Interact(gameManager);
        }

        // 3) Aperto e vuoto -> chiudi.
        StartSlide(closedPos, false);
        return string.Empty;
    }

    // --- Azione secondaria: chiudi senza raccogliere (solo se c'è un oggetto da prendere) ---

    public bool CanInteractSecondary() => isOpen && !animating && HasGrabbablePickup();

    public string GetSecondaryPrompt() => secondaryClosePrompt;

    public string InteractSecondary(GameManager gameManager)
    {
        if (animating || !isOpen)
        {
            return string.Empty;
        }

        StartSlide(closedPos, false);
        return string.Empty;
    }

    // È bloccato finché serve un oggetto e l'inventario non lo contiene.
    // Una volta aperto resta sbloccato (così resta apribile anche se l'oggetto viene consumato/scambiato).
    private bool IsLocked()
    {
        if (unlocked || string.IsNullOrWhiteSpace(requiredItemName))
        {
            return false;
        }

        PhysicalInventory inventory = ResolveInventory();
        return inventory == null || !inventory.HasItem(requiredItemName);
    }

    private void UnlockWithItem()
    {
        if (string.IsNullOrWhiteSpace(requiredItemName))
        {
            return;
        }

        unlocked = true;

        if (consumeItemOnOpen)
        {
            ResolveInventory()?.RemoveItem(requiredItemName);
        }
    }

    private PhysicalInventory ResolveInventory()
    {
        return PhysicalInventory.Instance != null
            ? PhysicalInventory.Instance
            : FindFirstObjectByType<PhysicalInventory>();
    }

    private bool HasGrabbablePickup()
    {
        return pickupInside != null
            && pickupInside.gameObject.activeInHierarchy
            && pickupInside.CanInteract();
    }

    private void StartSlide(Vector3 target, bool opening)
    {
        isOpen = opening;
        PlaySlideSound(opening);
        if (slideRoutine != null)
        {
            StopCoroutine(slideRoutine);
        }
        slideRoutine = StartCoroutine(SlideTo(target));
    }

    private void PlaySlideSound(bool opening)
    {
        AudioClip clip = opening ? openClip : closeClip;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    private IEnumerator SlideTo(Vector3 target)
    {
        animating = true;

        while (Vector3.SqrMagnitude(transform.localPosition - target) > 0.0000001f)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, target, speed * Time.deltaTime);
            yield return null;
        }

        transform.localPosition = target;
        animating = false;
        slideRoutine = null;
    }
}
