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
        EnsureRaycastCollider();

        // Alone oro/scintillante che distingue i raccoglibili dagli oggetti di scena.
        ItemHighlight.Ensure(gameObject, ItemHighlight.Kind.Pickup);
    }

    // Garantisce un collider per il raycast di interazione. Serve soprattutto agli oggetti
    // "rimessi giù" dall'inventario: lo snapshot non ha collider, quindi va creato qui.
    //
    // ATTENZIONE (editor vs build): una MeshCollider convex creata da codice richiede che
    // la mesh abbia "Read/Write Enabled" attivo. Nell'editor le mesh sono sempre leggibili
    // in memoria, quindi funziona; in una build le mesh importate hanno Read/Write DISATTIVO
    // di default, la MeshCollider convex non viene generata e l'oggetto diventa non
    // raccoglibile. È esattamente il caso "nell'editor si raccoglie, nella build no".
    // Se la mesh non è leggibile ripieghiamo su una BoxCollider basata sui bounds, che non
    // richiede accesso ai vertici e funziona identica in editor e in build.
    private void EnsureRaycastCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        var meshFilter = GetComponentInChildren<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh.isReadable)
            {
                var col = meshFilter.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;
            }
            else
            {
                AddBoxColliderFromBounds(meshFilter.gameObject, mesh.bounds);
            }
            return;
        }

        var skinned = GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinned != null)
        {
            AddBoxColliderFromBounds(skinned.gameObject, skinned.localBounds);
            return;
        }

        gameObject.AddComponent<BoxCollider>();
    }

    // Mesh.bounds e SkinnedMeshRenderer.localBounds sono metadati serializzati, disponibili
    // anche quando la mesh non è leggibile: la box risultante funziona in ogni build.
    private static void AddBoxColliderFromBounds(GameObject target, Bounds localBounds)
    {
        var box = target.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size   = localBounds.size;
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
