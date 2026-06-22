using System.Collections.Generic;
using UnityEngine;

public class ClassroomMutator : MonoBehaviour
{
    [Header("References")]
    public WindowManager windowManager;

    [Tooltip("I MeshRenderer dei muri che possono tingersi di rosso sangue")]
    public List<MeshRenderer> wallRenderers = new List<MeshRenderer>();

    [Tooltip("Il transform della lavagna (viene inclinata)")]
    public Transform blackboard;

    [Tooltip("La mappa appesa al muro (viene capovolta a testa in giù)")]
    public Transform mapTransform;

    [Tooltip("Le luci della stanza che diventano rosse (componenti Light)")]
    public List<Light> roomLights = new List<Light>();

    [Header("Settings")]
    public float blackboardTiltMax = 8f;

    [Tooltip("Colore verso cui i muri sfumano")]
    public Color wallTintColor = new Color(0.5f, 0f, 0f);
    [Tooltip("Quanto si avvicina al rosso ad ogni mutazione (1 = subito pieno)")]
    [Range(0f, 1f)] public float wallTintStep = 0.5f;

    [Tooltip("Colore verso cui le luci sfumano")]
    public Color lightTintColor = new Color(0.8f, 0f, 0f);
    [Range(0f, 1f)] public float lightTintStep = 0.6f;

    [Tooltip("Rotazione applicata alla mappa per capovolgerla. Regola l'asse se la mappa non si gira come previsto.")]
    public Vector3 mapFlipEuler = new Vector3(0f, 0f, 180f);

    [Header("State")]
    [SerializeField] private int mutationsApplied = 0;

    // Stato originale salvato per reset
    private Dictionary<MeshRenderer, Color> originalWallColors = new Dictionary<MeshRenderer, Color>();
    private Dictionary<Light, Color> originalLightColors = new Dictionary<Light, Color>();
    private Quaternion originalBlackboardRotation;
    private Quaternion originalMapRotation;
    private bool mapFlipped = false;

    private enum MutationType { TiltBlackboard, TintWall, DisappearWindow, FlipMap, TintLights }

    public int MutationsApplied => mutationsApplied;

    void Awake()
    {
        foreach (var wall in wallRenderers)
            if (wall != null) originalWallColors[wall] = wall.material.color;

        foreach (var light in roomLights)
            if (light != null) originalLightColors[light] = light.color;

        if (blackboard != null)
            originalBlackboardRotation = blackboard.rotation;

        if (mapTransform != null)
            originalMapRotation = mapTransform.rotation;
    }

    public void ApplyRandomMutation()
    {
        MutationType type = (MutationType)UnityEngine.Random.Range(0, 5);

        switch (type)
        {
            case MutationType.TiltBlackboard:
                TiltBlackboard();
                break;
            case MutationType.TintWall:
                TintRandomWall();
                break;
            case MutationType.DisappearWindow:
                DisappearRandomWindow();
                break;
            case MutationType.FlipMap:
                FlipMap();
                break;
            case MutationType.TintLights:
                TintLights();
                break;
        }

        mutationsApplied++;
    }

    private void TiltBlackboard()
    {
        if (blackboard == null) return;

        float tilt = UnityEngine.Random.Range(-blackboardTiltMax, blackboardTiltMax);
        if (Mathf.Abs(tilt) < 2f) tilt = 4f * Mathf.Sign(tilt);

        blackboard.rotation = originalBlackboardRotation * Quaternion.Euler(0f, 0f, tilt);
        Debug.Log($"[Mutator] Lavagna inclinata di {tilt:F1} gradi");
    }

    private void TintRandomWall()
    {
        if (wallRenderers.Count == 0) return;

        MeshRenderer target = wallRenderers[UnityEngine.Random.Range(0, wallRenderers.Count)];
        if (target == null) return;

        Color current = target.material.color;
        target.material.color = Color.Lerp(current, wallTintColor, wallTintStep);
        Debug.Log($"[Mutator] Muro tinto di rosso: {target.gameObject.name}");
    }

    private void DisappearRandomWindow()
    {
        if (windowManager == null) return;
        windowManager.DisappearRandomWindow();
    }

    private void FlipMap()
    {
        if (mapTransform == null) return;

        mapFlipped = !mapFlipped;
        mapTransform.rotation = mapFlipped
            ? originalMapRotation * Quaternion.Euler(mapFlipEuler)
            : originalMapRotation;
        Debug.Log($"[Mutator] Mappa {(mapFlipped ? "capovolta a testa in giù" : "raddrizzata")}");
    }

    private void TintLights()
    {
        if (roomLights.Count == 0) return;

        bool any = false;
        foreach (var light in roomLights)
        {
            if (light == null) continue;
            light.color = Color.Lerp(light.color, lightTintColor, lightTintStep);
            any = true;
        }
        if (any) Debug.Log("[Mutator] Luci tinte di rosso");
    }

    public void ResetAllMutations()
    {
        foreach (var pair in originalWallColors)
            if (pair.Key != null) pair.Key.material.color = pair.Value;

        foreach (var pair in originalLightColors)
            if (pair.Key != null) pair.Key.color = pair.Value;

        if (blackboard != null)
            blackboard.rotation = originalBlackboardRotation;

        if (mapTransform != null)
            mapTransform.rotation = originalMapRotation;
        mapFlipped = false;

        mutationsApplied = 0;
    }
}
