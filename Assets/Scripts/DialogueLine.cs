using System;
using UnityEngine;

[Serializable]
public struct DialogueLine
{
    public enum Speaker { Player, NPC }

    public Speaker speaker;

    [TextArea(2, 4)]
    public string text;
}
