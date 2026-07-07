using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Un singolo stadio di potenziamento di un oggetto: consumando 'requiredItem'
// dall'inventario, l'oggetto assume questo nome/descrizione/modello.
[Serializable]
public class ItemUpgradeStage
{
    [Tooltip("Oggetto consumato dall'inventario per raggiungere questo stadio (es. 'Occhio').")]
    public string requiredItem = "Occhio";

    [Tooltip("Nome dell'oggetto una volta raggiunto questo stadio (es. 'Bambola con un occhio').")]
    public string name = "";

    [TextArea(2, 3)]
    [Tooltip("Descrizione mostrata nell'inventario in questo stadio.")]
    public string description = "";

    [Tooltip("Modello 3D mostrato nell'inventario in questo stadio. Può essere un prefab o direttamente l'FBX.")]
    public GameObject model;

    public bool canInspect = true;

    [Min(0.05f)]
    public float inspectionScaleMultiplier = 1f;

    // Gli elementi aggiunti a un array da Inspector NON ereditano i default C#: restano a
    // 0/""/false. Questi accessor rendono innocui i valori non impostati, così uno stadio
    // appena aggiunto non finisce microscopico (scala 0) o non-ispezionabile per sbaglio.
    public float EffectiveInspectionScale => inspectionScaleMultiplier > 0.001f ? inspectionScaleMultiplier : 1f;
    public bool HasRequiredItem => !string.IsNullOrWhiteSpace(requiredItem);
}

// Catena di potenziamenti applicabile a un oggetto dell'inventario. Ogni passo consuma
// un oggetto e cambia modello/nome (es. inserire occhi nel peluche: peluche senza occhi
// -> bambola con un occhio -> bambola con due occhi). Lo stadio base è l'oggetto stesso
// al momento della consegna; gli 'stages' sono i potenziamenti successivi, in ordine.
//
// L'avanzamento (nextStageIndex) vive sul CollectedItem così sopravvive ai rebuild del
// carosello d'inventario e al drop/ripresa dell'oggetto.
[Serializable]
public class ItemUpgradePath
{
    [Tooltip("Tasto premuto durante l'ispezione per applicare il potenziamento.")]
    public Key actionKey = Key.Space;

    [Tooltip("Etichetta dell'azione mostrata nella barra suggerimenti.")]
    public string actionLabel = "Inserisci occhio";

    [Tooltip("Stadi successivi, in ordine. Vuoto = nessun potenziamento.")]
    public ItemUpgradeStage[] stages = Array.Empty<ItemUpgradeStage>();

    [HideInInspector]
    public int nextStageIndex = 0;

    public bool IsConfigured => stages != null && stages.Length > 0;
    public bool HasNext => stages != null && nextStageIndex >= 0 && nextStageIndex < stages.Length;
    public ItemUpgradeStage Next => HasNext ? stages[nextStageIndex] : null;

    // Copia da assegnare a un CollectedItem, così che l'avanzamento a runtime non muti
    // l'asset configurato nell'inspector. Gli stadi (sola lettura) restano condivisi.
    public ItemUpgradePath Clone()
    {
        return new ItemUpgradePath
        {
            actionKey = actionKey,
            actionLabel = actionLabel,
            stages = stages,
            nextStageIndex = 0
        };
    }
}
