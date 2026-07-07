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

    // Catena di potenziamento trasportata a runtime: serve a non perdere lo stato (es.
    // bambola con un occhio) quando l'oggetto viene lasciato e poi ripreso.
    [HideInInspector] public ItemUpgradePath upgradePath;

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
        GameObject snapshot = InventorySnapshot.Create(source, itemName);

        inventory.AddItem(new CollectedItem
        {
            inventoryId  = GetInventoryId(),
            name        = itemName,
            description = description,
            canInspect  = canInspect,
            inspectionScaleMultiplier = inspectionScaleMultiplier,
            worldSource = snapshot,
            upgradePath = upgradePath
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
