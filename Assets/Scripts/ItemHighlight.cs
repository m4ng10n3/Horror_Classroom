using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Alone scintillante per distinguere a colpo d'occhio gli oggetti interagibili.
// Non tocca i materiali originali: per ogni mesh dell'oggetto crea un figlio "overlay"
// che ridisegna la stessa mesh con lo shader additivo Custom/ItemHighlight (rim + sparkle).
//
// Due profili visivamente distinti:
//   - Pickup    -> oro/ambra, luccichio calmo  (raccolta immediata, sicura)
//   - Stealable -> cremisi/rosso, sfarfallio rapido e pulsante (furto, appartiene a qualcuno)
//
// Viene agganciato automaticamente da PickupItem e StealableItem in Awake, quindi non
// serve alcun wiring nell'Inspector. Quando l'oggetto viene raccolto/rubato e disattivato
// (SetActive(false)), OnDisable rimuove gli overlay: nessun alone residuo in scena.
[DisallowMultipleComponent]
public class ItemHighlight : MonoBehaviour
{
    public enum Kind { Pickup, Stealable }

    private const string ShaderName = "Custom/ItemHighlight";

    [Tooltip("Profilo cromatico: Pickup = oro calmo, Stealable = rosso agitato.")]
    [SerializeField] private Kind kind = Kind.Pickup;

    [Min(0f)]
    [Tooltip("Moltiplica l'intensità di rim e sparkle (1 = default).")]
    [SerializeField] private float intensityMultiplier = 0.25f;

    private static Shader cachedShader;
    // Un materiale condiviso per profilo: le sparkle variano comunque nello spazio, quindi
    // ogni oggetto scintilla in modo diverso pur riusando lo stesso materiale (2 in totale).
    private static readonly Dictionary<Kind, Material> sharedMaterials = new Dictionary<Kind, Material>();
    private static bool warnedMissingShader;

    private readonly List<GameObject> overlays = new List<GameObject>();

    public Kind HighlightKind
    {
        get => kind;
        set
        {
            if (kind == value) return;
            kind = value;
            if (isActiveAndEnabled) Rebuild();
        }
    }

    // Punto d'ingresso usato dagli item: aggiunge il componente se manca e imposta il profilo.
    public static ItemHighlight Ensure(GameObject go, Kind kind)
    {
        if (go == null) return null;
        var highlight = go.GetComponent<ItemHighlight>();
        if (highlight == null) highlight = go.AddComponent<ItemHighlight>();
        highlight.HighlightKind = kind;
        return highlight;
    }

    void OnEnable() => Rebuild();
    void OnDisable() => Clear();
    void OnDestroy() => Clear();

    // Mostra/nasconde l'alone senza ricostruirlo (utile se in futuro lo si vuole
    // accendere solo da vicino).
    public void SetVisible(bool visible)
    {
        foreach (var overlay in overlays)
            if (overlay != null) overlay.SetActive(visible);
    }

    private void Rebuild()
    {
        Clear();

        Material overlayMaterial = GetSharedMaterial(kind);
        if (overlayMaterial == null) return;

        foreach (var meshFilter in GetComponentsInChildren<MeshFilter>(true))
        {
            // Salta gli overlay già creati da noi (evita di evidenziare l'evidenziazione).
            if (meshFilter.GetComponent<ItemHighlightOverlay>() != null) continue;

            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) continue;

            var sourceRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null || !sourceRenderer.enabled) continue;

            CreateOverlay(meshFilter, mesh, overlayMaterial);
        }
    }

    private void CreateOverlay(MeshFilter source, Mesh mesh, Material overlayMaterial)
    {
        var overlayGo = new GameObject("__HighlightOverlay");
        overlayGo.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        overlayGo.layer = source.gameObject.layer;

        // Figlio con trasformazione identità: ricalca esattamente la mesh sorgente.
        Transform t = overlayGo.transform;
        t.SetParent(source.transform, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        overlayGo.AddComponent<ItemHighlightOverlay>();

        var overlayFilter = overlayGo.AddComponent<MeshFilter>();
        overlayFilter.sharedMesh = mesh;

        var overlayRenderer = overlayGo.AddComponent<MeshRenderer>();

        // Un materiale per sotto-mesh, così l'alone copre l'intera mesh (anche multi-materiale).
        int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
        var materials = new Material[subMeshCount];
        for (int i = 0; i < subMeshCount; i++) materials[i] = overlayMaterial;
        overlayRenderer.sharedMaterials = materials;

        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
        overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlayRenderer.allowOcclusionWhenDynamic = false;

        // Intensità per-oggetto senza toccare il materiale condiviso: scala rim e sparkle
        // partendo dai valori base del preset. Applicato solo se diverso da 1 per non
        // rompere inutilmente l'SRP Batcher.
        if (!Mathf.Approximately(intensityMultiplier, 1f))
        {
            var block = new MaterialPropertyBlock();
            block.SetFloat("_RimStrength", overlayMaterial.GetFloat("_RimStrength") * intensityMultiplier);
            block.SetFloat("_SparkleStrength", overlayMaterial.GetFloat("_SparkleStrength") * intensityMultiplier);
            overlayRenderer.SetPropertyBlock(block);
        }

        overlays.Add(overlayGo);
    }

    private void Clear()
    {
        foreach (var overlay in overlays)
            if (overlay != null) Destroy(overlay);
        overlays.Clear();
    }

    private static Material GetSharedMaterial(Kind kind)
    {
        if (sharedMaterials.TryGetValue(kind, out Material existing) && existing != null)
            return existing;

        Shader shader = ResolveShader();
        if (shader == null)
        {
            if (!warnedMissingShader)
            {
                Debug.LogWarning($"[ItemHighlight] Shader '{ShaderName}' non trovato: nessun alone verrà mostrato.");
                warnedMissingShader = true;
            }
            return null;
        }

        var material = new Material(shader) { hideFlags = HideFlags.DontSave };
        ApplyStyle(material, kind);
        sharedMaterials[kind] = material;
        return material;
    }

    private static Shader ResolveShader()
    {
        if (cachedShader != null) return cachedShader;
        cachedShader = Shader.Find(ShaderName);
        if (cachedShader == null) cachedShader = Resources.Load<Shader>("ItemHighlight");
        return cachedShader;
    }

    private static void ApplyStyle(Material material, Kind kind)
    {
        if (kind == Kind.Stealable)
        {
            // Rosso cremisi, lampeggio veloce e battito marcato: "attenzione, è di qualcuno".
            // Rim sottile (RimPower alto): il corpo texturizzato resta ben visibile.
            material.SetColor("_Color", new Color(1.0f, 0.12f, 0.14f, 1f));
            material.SetFloat("_RimPower", 3.5f);
            material.SetFloat("_RimStrength", 1.5f);
            material.SetColor("_SparkleColor", new Color(1.0f, 0.40f, 0.25f, 1f));
            material.SetFloat("_SparkleScale", 70f);
            material.SetFloat("_SparkleSpeed", 4.0f);   // lampeggia più spesso
            material.SetFloat("_SparkleDensity", 0.975f);
            material.SetFloat("_SparkleStrength", 2.6f);
            material.SetFloat("_Pulse", 3.0f);
            material.SetFloat("_PulseAmount", 0.40f);
        }
        else
        {
            // Oro/ambra, lampeggio calmo: "raccoglibile in sicurezza".
            // Rim sottile: mostra la texture originale, con solo il bordo che pulsa.
            material.SetColor("_Color", new Color(1.0f, 0.78f, 0.30f, 1f));
            material.SetFloat("_RimPower", 4.0f);
            material.SetFloat("_RimStrength", 1.3f);
            material.SetColor("_SparkleColor", new Color(1.0f, 0.95f, 0.70f, 1f));
            material.SetFloat("_SparkleScale", 55f);
            material.SetFloat("_SparkleSpeed", 2.0f);   // lampeggio lento
            material.SetFloat("_SparkleDensity", 0.985f);
            material.SetFloat("_SparkleStrength", 2.2f);
            material.SetFloat("_Pulse", 1.2f);
            material.SetFloat("_PulseAmount", 0.25f);
        }
    }
}

// Marcatore sui figli overlay generati da ItemHighlight: serve a riconoscerli (per non
// evidenziarli di nuovo) e a rimuoverli dagli snapshot dell'inventario.
public class ItemHighlightOverlay : MonoBehaviour { }
