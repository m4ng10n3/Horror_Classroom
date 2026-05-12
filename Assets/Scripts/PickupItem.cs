using UnityEngine;

public class PickupItem : MonoBehaviour, IPlayerInteractable
{
    [Header("Identità")]
    public string itemName = "Oggetto";

    [TextArea(2, 3)]
    public string description = "Un oggetto trovato in aula.";

    [Header("Ispezione 3D")]
    public bool canInspect = true;

    [Min(0.05f)]
    [Tooltip("Moltiplica la dimensione dell'oggetto nella visualizzazione inventario. 1 = default, 2 = doppio.")]
    public float inspectionScaleMultiplier = 1f;

    [Tooltip("Modello alternativo (lascia vuoto per usare questo oggetto).")]
    public GameObject inspectionModelOverride;

    [Header("Integrazione Inventario Logico")]
    [Tooltip("Se true aggiunge itemName all'EscapeInventory per i baratti.")]
    public bool addToEscapeInventory = false;

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

    public bool CanInteract(EscapeInventory inventory) => !pickedUp && isActiveAndEnabled;
    public string GetInteractionPrompt(EscapeInventory inventory) => $"[F] Raccogli {itemName}";

    public string Interact(EscapeInventory inventory, GameManager gameManager)
    {
        if (pickedUp) return string.Empty;
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

        PhysicalInventory.Instance?.AddItem(new CollectedItem
        {
            name        = itemName,
            description = description,
            canInspect  = canInspect,
            inspectionScaleMultiplier = inspectionScaleMultiplier,
            worldSource = snapshot
        });

        if (addToEscapeInventory && inventory != null)
            inventory.AddRawItem(itemName);

        gameObject.SetActive(false);

        return $"Raccolto: {itemName}";
    }
}
