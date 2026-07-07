using System;
using UnityEngine;

[Serializable]
public class CollectedItem
{
    public string inventoryId;
    public string name;
    public string description;
    public bool canInspect;
    public float inspectionScaleMultiplier = 1f;
    public GameObject worldSource;

    // Catena di potenziamento opzionale (es. inserire occhi nel peluche). Null per gli
    // oggetti normali. Lo stato di avanzamento vive qui, così segue l'oggetto.
    public ItemUpgradePath upgradePath;
}
