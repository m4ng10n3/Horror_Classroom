using UnityEngine;

// Oggetto rubabile: appartiene a uno StudentNPC presente in scena. A differenza del
// pickup istantaneo (PickupItem), richiede di tenere premuto [F] per stealDuration
// secondi, fa salire la barra di esposizione e, al completamento, aggiunge sospetto
// permanente e avvisa il proprietario. Quando il proprietario sparisce, l'oggetto
// viene convertito in un normale PickupItem gratuito.
[DisallowMultipleComponent]
public class StealableItem : MonoBehaviour, IHoldInteractable
{
    [Header("Identità")]
    [Tooltip("Nome dell'oggetto, mostrato nel prompt e salvato in inventario.")]
    public string itemName = "Oggetto";

    [TextArea(2, 3)]
    [Tooltip("Descrizione mostrata nella scheda inventario.")]
    public string itemDescription = "Un oggetto preso di nascosto dal banco di qualcuno.";

    [Header("Furto")]
    [Min(0.05f)]
    [Tooltip("Secondi di pressione continua necessari per completare il furto.")]
    public float stealDuration = 2f;

    [Min(0f)]
    [Tooltip("Quanto riempie al secondo la barra di esposizione durante il furto. Oggetti piccoli pochissimo, oggetti grossi/rumorosi molto.")]
    public float noiseRate = 0.6f;

    [Min(0f)]
    [Tooltip("Sospetto PERMANENTE aggiunto al completamento. Non si svuota mai, a differenza della barra di esposizione.")]
    public float suspicionCost = 1f;

    [Header("Proprietario")]
    [Tooltip("Lo studente a cui appartiene l'oggetto: viene avvisato (Witness) quando il furto riesce. Impostandolo qui l'oggetto si registra da solo per la conversione a pickup gratuito.")]
    public StudentNPC owner;

    [Header("Ispezione 3D")]
    [Tooltip("Se true l'oggetto è ispezionabile dal Tab una volta in inventario.")]
    public bool canInspect = true;

    [Min(0.05f)]
    [Tooltip("Moltiplica la dimensione dell'oggetto nella visualizzazione inventario. 1 = default, 2 = doppio.")]
    public float inspectionScaleMultiplier = 1f;

    [Tooltip("Modello alternativo da mostrare nell'inventario (lascia vuoto per usare questo oggetto).")]
    public GameObject inspectionModelOverride;

    private bool stolen = false;
    private FPSController cachedPlayer;
    private SuspicionCounter cachedSuspicion;

    public float HoldDuration => stealDuration;

    void Awake()
    {
        // Stesso accorgimento di PickupItem: serve un collider per il raycast di interazione.
        if (GetComponentInChildren<Collider>() == null)
        {
            var mesh = GetComponentInChildren<MeshFilter>();
            if (mesh != null)
            {
                var col = gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mesh.sharedMesh;
                col.convex = true;
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }

        if (owner != null)
        {
            owner.RegisterStealable(this);
        }
    }

    public bool CanInteract() => !stolen && isActiveAndEnabled;

    public string GetInteractionPrompt() => $"[F] Ruba {itemName} (tieni premuto)";

    // Fallback: se per qualche motivo venisse trattato come tap istantaneo, non ruba.
    public string Interact(GameManager gameManager) => $"Tieni premuto [F] per rubare {itemName}.";

    public bool CanBeginHold(out string blockReason)
    {
        blockReason = string.Empty;
        if (stolen) return false;

        PhysicalInventory inventory = ResolveInventory();
        if (inventory == null)
        {
            blockReason = "Inventario fisico non trovato.";
            return false;
        }

        // Come per i baratti di StudentNPC: l'inventario pieno blocca l'azione PRIMA
        // di iniziare il hold, non a metà.
        if (inventory.IsFull)
        {
            blockReason = $"Inventario pieno! Lascia qualcosa prima di rubare {itemName}.";
            return false;
        }

        return true;
    }

    public bool OnHoldTick(float deltaTime, float progress01)
    {
        FPSController player = ResolvePlayer();
        if (player != null)
        {
            // Se l'esposizione è già al massimo, non aggiungere altro: l'evento di
            // "scoperto" lo gestisce il FPSController (OnRunLimitExceeded). Qui si annulla.
            if (player.IsExposureMaxed)
                return false;

            player.AddExposure(noiseRate * deltaTime);

            // Appena raggiunto il massimo durante il furto: interrompi. AddExposure ha
            // già fatto scattare l'evento di "scoperto" esistente.
            if (player.IsExposureMaxed)
            {
                BackgroundMusicController.Instance?.SetStealing(false);
                VignetteController.Instance?.SetStealing(false);
                return false;
            }
        }

        // Furto in corso: accelera la musica e fa crescere la vignettatura col progresso.
        BackgroundMusicController.Instance?.SetStealing(true);
        VignetteController.Instance?.SetStealing(true, progress01);

        return true;
    }

    public void OnHoldCompleted(GameManager gameManager)
    {
        if (stolen) return;

        PhysicalInventory inventory = ResolveInventory();
        if (inventory == null || inventory.IsFull) return; // doppia sicurezza

        stolen = true;

        AddToInventory(inventory);
        AddSuspicion();

        if (owner != null)
            owner.Witness();

        // Furto concluso: la musica torna alla velocità base e la vignettatura svanisce.
        BackgroundMusicController.Instance?.SetStealing(false);
        VignetteController.Instance?.SetStealing(false);

        gameObject.SetActive(false);
        Debug.Log($"[Steal] Rubato {itemName} a {(owner != null ? owner.SpeakerName : "nessuno")}.");
    }

    public void OnHoldCancelled()
    {
        // Nessuna penalità: niente oggetto, niente sospetto. La barra di esposizione
        // smette di salire e torna a scendere secondo le regole del FPSController.
        // La musica rallenta tornando alla velocità base e la vignettatura svanisce.
        BackgroundMusicController.Instance?.SetStealing(false);
        VignetteController.Instance?.SetStealing(false);
    }

    // Converte l'oggetto in un pickup gratuito (niente hold, esposizione o sospetto),
    // riusando la classe PickupItem esistente. Chiamato da StudentNPC.Disappear().
    public void ConvertToFreePickup()
    {
        if (stolen) return; // già rubato: non c'è nulla da convertire

        PickupItem pickup = gameObject.GetComponent<PickupItem>();
        if (pickup == null)
            pickup = gameObject.AddComponent<PickupItem>();

        pickup.itemName = itemName;
        pickup.description = itemDescription;
        pickup.canInspect = canInspect;
        pickup.inspectionScaleMultiplier = inspectionScaleMultiplier;
        pickup.inspectionModelOverride = inspectionModelOverride;

        // Rimuovi la logica di furto: un componente StealableItem disabilitato
        // schermerebbe comunque PickupItem nel raycast di interazione, quindi va distrutto.
        Destroy(this);
    }

    private void AddToInventory(PhysicalInventory inventory)
    {
        GameObject source = inspectionModelOverride != null ? inspectionModelOverride : gameObject;

        // Snapshot attivo fuori dal far clip plane, identico al pattern di PickupItem/StudentNPC.
        GameObject snapshot = Instantiate(source);
        snapshot.name = "__snapshot_" + itemName;
        snapshot.transform.position = new Vector3(0f, -5000f, 0f);
        snapshot.transform.rotation = Quaternion.identity;
        snapshot.SetActive(true);

        foreach (var col in snapshot.GetComponentsInChildren<Collider>())
            Destroy(col);
        foreach (var st in snapshot.GetComponentsInChildren<StealableItem>())
            Destroy(st);
        foreach (var pu in snapshot.GetComponentsInChildren<PickupItem>())
            Destroy(pu);

        DontDestroyOnLoad(snapshot);

        inventory.AddItem(new CollectedItem
        {
            name        = itemName,
            description = itemDescription,
            canInspect  = canInspect,
            inspectionScaleMultiplier = inspectionScaleMultiplier,
            worldSource = snapshot
        });
    }

    private void AddSuspicion()
    {
        if (suspicionCost <= 0f) return;

        SuspicionCounter counter = ResolveSuspicion();
        if (counter == null) return;

        // SuspicionCounter lavora con interi: arrotondiamo, garantendo almeno +1
        // visto che il furto ha un costo dichiarato. Il valore non si svuota mai da solo.
        int amount = Mathf.Max(1, Mathf.RoundToInt(suspicionCost));
        counter.Increase(amount, $"Furto: {itemName}");
    }

    private PhysicalInventory ResolveInventory()
    {
        return PhysicalInventory.Instance != null
            ? PhysicalInventory.Instance
            : FindFirstObjectByType<PhysicalInventory>();
    }

    private FPSController ResolvePlayer()
    {
        if (cachedPlayer == null)
            cachedPlayer = FindFirstObjectByType<FPSController>();
        return cachedPlayer;
    }

    private SuspicionCounter ResolveSuspicion()
    {
        if (cachedSuspicion == null)
            cachedSuspicion = FindFirstObjectByType<SuspicionCounter>();
        return cachedSuspicion;
    }
}
