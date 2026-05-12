using System;
using UnityEngine;

[Serializable]
public class CollectedItem
{
    public string name;
    public string description;
    public bool canInspect;
    public GameObject worldSource;
}
