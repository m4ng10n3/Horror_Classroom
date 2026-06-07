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
}
