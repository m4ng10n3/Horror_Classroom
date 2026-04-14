using UnityEngine;

public class StudentNPC : MonoBehaviour
{
    [Header("Identity")]
    public string studentName;
    public Color studentColor = Color.white;

    [Header("Trade System (Fase 7)")]
    [Tooltip("Se true, questo studente è legato a un baratto e non può sparire finché non l'ha fatto")]
    public bool essential = false;

    [Tooltip("Diventa true quando lo scambio è stato completato")]
    public bool tradeDone = false;

    [Header("State")]
    [SerializeField] private bool isVisible = true;

    private MeshRenderer bodyRenderer;
    private Material bodyMaterialInstance;

    void Awake()
    {
        // Cerca il renderer sulla capsula figlia
        bodyRenderer = GetComponentInChildren<MeshRenderer>();
        if (bodyRenderer != null)
        {
            bodyMaterialInstance = bodyRenderer.material;
            bodyMaterialInstance.color = studentColor;
        }
    }

    /// <summary>
    /// Può sparire? Solo se non è essential, oppure se è essential ma ha già fatto il baratto
    /// </summary>
    public bool CanDisappear()
    {
        if (!isVisible) return false;
        if (essential && !tradeDone) return false;
        return true;
    }

    public bool IsVisible => isVisible;

    public void Disappear()
    {
        isVisible = false;
        gameObject.SetActive(false);
        Debug.Log($"[Student] {studentName} è sparito...");
    }

    public void Reappear()
    {
        isVisible = true;
        gameObject.SetActive(true);
    }
}