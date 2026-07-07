using UnityEngine;

// Crea uno "snapshot" 3D di un oggetto da mostrare nell'inventario fisico: un clone
// attivo, spostato fuori dalla vista (y = -5000), senza collider né PickupItem, e
// persistente fra le scene. Condiviso da PickupItem, StudentNPC e
// ItemInspectionController per non duplicare la stessa logica.
public static class InventorySnapshot
{
    public static GameObject Create(GameObject source, string itemName)
    {
        if (source == null) return null;

        GameObject snapshot = Object.Instantiate(source);
        snapshot.name = "__snapshot_" + (string.IsNullOrWhiteSpace(itemName) ? source.name : itemName);
        snapshot.transform.position = new Vector3(0f, -5000f, 0f);
        snapshot.transform.rotation = Quaternion.identity;
        snapshot.SetActive(true);

        // I collider non servono per l'ispezione; PickupItem e StealableItem vanno rimossi
        // dal clone per evitare che resti interagibile (doppio raccoglimento/furto).
        foreach (var col in snapshot.GetComponentsInChildren<Collider>())
            Object.Destroy(col);
        foreach (var pu in snapshot.GetComponentsInChildren<PickupItem>())
            Object.Destroy(pu);
        foreach (var st in snapshot.GetComponentsInChildren<StealableItem>())
            Object.Destroy(st);

        Object.DontDestroyOnLoad(snapshot);
        return snapshot;
    }
}
