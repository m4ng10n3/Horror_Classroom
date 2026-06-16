using UnityEngine;

public class PickupItem : MonoBehaviour, IPlayerInteractable
{
    [Header("Identità")]
    public string itemName = "Oggetto";

    [Tooltip("ID usato da DoorEscape per riconoscere questo pickup. Se vuoto, viene usato l'ID runtime dell'oggetto in scena.")]
    public string itemId = "";

    [TextArea(2, 3)]
    public string description = "Un oggetto trovato in aula.";

    [Header("Ispezione 3D")]
    public bool canInspect = true;

    [Min(0.05f)]
    [Tooltip("Moltiplica la dimensione dell'oggetto nella visualizzazione inventario. 1 = default, 2 = doppio.")]
    public float inspectionScaleMultiplier = 1f;

    [Tooltip("Modello alternativo (lascia vuoto per usare questo oggetto).")]
    public GameObject inspectionModelOverride;

    private bool pickedUp = false;

    void Awake()
    {
        // Aggiunge collider automatico se mancante (necessario per il raycast)
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
    }

    public bool CanInteract() => !pickedUp && isActiveAndEnabled;
    public string GetInteractionPrompt() => $"[F] Raccogli {itemName}";

    public string Interact(GameManager gameManager)
    {
        if (pickedUp) return string.Empty;

        PhysicalInventory inventory = PhysicalInventory.Instance != null
            ? PhysicalInventory.Instance
            : FindFirstObjectByType<PhysicalInventory>();

        if (inventory == null)
        {
            return "Inventario fisico non trovato.";
        }

        if (inventory.IsFull)
        {
            return $"Inventario pieno! Puoi portare al massimo {inventory.maxCapacity} oggetti.";
        }

        pickedUp = true;

        GameObject source = inspectionModelOverride != null ? inspectionModelOverride : gameObject;

        // Crea snapshot ATTIVO e posizionalo a y=-5000, fuori dal far clip plane di qualsiasi camera.
        // Un oggetto attivo permette a Instantiate di creare un clone attivo e visibile.
        GameObject snapshot = Instantiate(source);
        snapshot.name = "__snapshot_" + itemName;
        snapshot.transform.position = new Vector3(0f, -5000f, 0f);
        snapshot.transform.rotation = Quaternion.identity;
        snapshot.SetActive(true);

        // Rimuovi collider dal clone (non servono per l'ispezione)
        foreach (var col in snapshot.GetComponentsInChildren<Collider>())
            Destroy(col);
        // Rimuovi solo PickupItem per evitare doppio raccoglimento
        foreach (var pu in snapshot.GetComponentsInChildren<PickupItem>())
            Destroy(pu);

        DontDestroyOnLoad(snapshot);

        inventory.AddItem(new CollectedItem
        {
            inventoryId  = GetInventoryId(),
            name        = itemName,
            description = description,
            canInspect  = canInspect,
            inspectionScaleMultiplier = inspectionScaleMultiplier,
            worldSource = snapshot
        });

        gameObject.SetActive(false);

        return $"Raccolto: {itemName}";
    }

    public string GetInventoryId()
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? GetInstanceID().ToString()
            : itemId.Trim();
    }
}
