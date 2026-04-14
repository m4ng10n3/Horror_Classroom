using System.Collections.Generic;
using UnityEngine;

public class ClassroomMutator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("I banchi degli studenti (tutti tranne quello del player)")]
    public List<GameObject> studentDesks = new List<GameObject>();
    public WindowManager windowManager;


    [Tooltip("I MeshRenderer dei muri")]
    public List<MeshRenderer> wallRenderers = new List<MeshRenderer>();

    [Tooltip("Il transform della lavagna")]
    public Transform blackboard;

    [Header("Settings")]
    public float deskShiftAmount = 0.3f;
    public float blackboardTiltMax = 8f;
    public Color wallTintColor = new Color(0.6f, 0.15f, 0.15f);

    [Header("State")]
    [SerializeField] private int mutationsApplied = 0;

    // Stato originale salvato per reset
    private Dictionary<GameObject, Vector3> originalDeskPositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<MeshRenderer, Color> originalWallColors = new Dictionary<MeshRenderer, Color>();
    private Quaternion originalBlackboardRotation;

    private enum MutationType { MoveDeskSlightly, TintWall, TiltBlackboard, RotateDesk, DisappearWindow }

    public int MutationsApplied => mutationsApplied;

    void Awake()
    {
        foreach (var desk in studentDesks)
        {
            if (desk != null)
                originalDeskPositions[desk] = desk.transform.position;
        }

        foreach (var wall in wallRenderers)
        {
            if (wall != null)
                originalWallColors[wall] = wall.material.color;
        }

        if (blackboard != null)
            originalBlackboardRotation = blackboard.rotation;
    }

    public void ApplyRandomMutation()
    {
        MutationType type = (MutationType)UnityEngine.Random.Range(0, 5);

        switch (type)
        {
            case MutationType.MoveDeskSlightly:
                MoveDeskSlightly();
                break;
            case MutationType.TintWall:
                TintRandomWall();
                break;
            case MutationType.TiltBlackboard:
                TiltBlackboard();
                break;
            case MutationType.RotateDesk:
                RotateRandomDesk();
                break;
            case MutationType.DisappearWindow:
                DisappearRandomWindow();
                break;
        }

        mutationsApplied++;
    }

    private void MoveDeskSlightly()
    {
        List<GameObject> active = new List<GameObject>();
        foreach (var d in studentDesks)
            if (d != null && d.activeSelf) active.Add(d);

        if (active.Count == 0) return;

        GameObject target = active[UnityEngine.Random.Range(0, active.Count)];
        Vector3 offset = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            0f,
            UnityEngine.Random.Range(-1f, 1f)
        ).normalized * deskShiftAmount;

        target.transform.position += offset;
        Debug.Log($"[Mutator] Banco spostato: {target.name}");
    }

    private void TintRandomWall()
    {
        if (wallRenderers.Count == 0) return;

        MeshRenderer target = wallRenderers[UnityEngine.Random.Range(0, wallRenderers.Count)];
        if (target == null) return;

        Color current = target.material.color;
        target.material.color = Color.Lerp(current, wallTintColor, 0.3f);
        Debug.Log($"[Mutator] Muro tinto: {target.gameObject.name}");
    }

    private void TiltBlackboard()
    {
        if (blackboard == null) return;

        float tilt = UnityEngine.Random.Range(-blackboardTiltMax, blackboardTiltMax);
        if (Mathf.Abs(tilt) < 2f) tilt = 4f * Mathf.Sign(tilt);

        blackboard.rotation = originalBlackboardRotation * Quaternion.Euler(0f, 0f, tilt);
        Debug.Log($"[Mutator] Lavagna inclinata di {tilt:F1} gradi");
    }

    private void RotateRandomDesk()
    {
        List<GameObject> active = new List<GameObject>();
        foreach (var d in studentDesks)
            if (d != null && d.activeSelf) active.Add(d);

        if (active.Count == 0) return;

        GameObject target = active[UnityEngine.Random.Range(0, active.Count)];
        float angle = UnityEngine.Random.Range(10f, 35f);
        if (UnityEngine.Random.value > 0.5f) angle = -angle;

        target.transform.Rotate(0f, angle, 0f);
        Debug.Log($"[Mutator] Banco ruotato: {target.name} di {angle:F1} gradi");
    }

    private void DisappearRandomWindow()
    {
        if (windowManager == null) return;
        windowManager.DisappearRandomWindow();
    }

    public void ResetAllMutations()
    {
        foreach (var pair in originalDeskPositions)
            if (pair.Key != null) pair.Key.transform.position = pair.Value;

        foreach (var pair in originalWallColors)
            if (pair.Key != null) pair.Key.material.color = pair.Value;

        if (blackboard != null)
            blackboard.rotation = originalBlackboardRotation;

        // Reset rotazione banchi
        foreach (var desk in studentDesks)
            if (desk != null) desk.transform.rotation = Quaternion.identity;

        mutationsApplied = 0;
    }
}