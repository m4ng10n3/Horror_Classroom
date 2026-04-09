using UnityEngine;

public class TeacherVisuals : MonoBehaviour
{
    [Header("References")]
    public TeacherStateMachine stateMachine;
    public Light ambientLight;
    public MeshRenderer teacherBodyRenderer;

    [Header("Neutral State")]
    public Color neutralLightColor = new Color(1f, 0.96f, 0.88f);
    public Color neutralBodyColor = new Color(0.24f, 0.12f, 0.12f);
    public float neutralLightIntensity = 1.2f;

    [Header("Pleased State")]
    public Color pleasedLightColor = new Color(1f, 0.85f, 0.4f);
    public Color pleasedBodyColor = new Color(0.5f, 0.3f, 0.2f);
    public float pleasedLightIntensity = 1.6f;

    [Header("Angry State")]
    public Color angryLightColor = new Color(0.8f, 0.3f, 0.3f);
    public Color angryBodyColor = new Color(0.15f, 0.05f, 0.05f);
    public float angryLightIntensity = 0.7f;

    [Header("Transition")]
    public float transitionSpeed = 2f;

    private Color targetLightColor;
    private Color targetBodyColor;
    private float targetLightIntensity;
    private Material bodyMaterialInstance;

    void Start()
    {
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged += HandleStateChanged;
        }

        // Crea un'istanza del materiale per non modificare l'asset originale
        if (teacherBodyRenderer != null)
        {
            bodyMaterialInstance = teacherBodyRenderer.material;
        }

        // Imposta i valori iniziali (Neutral di default)
        targetLightColor = neutralLightColor;
        targetBodyColor = neutralBodyColor;
        targetLightIntensity = neutralLightIntensity;
    }

    void OnDestroy()
    {
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged -= HandleStateChanged;
        }
    }

    void Update()
    {
        if (ambientLight != null)
        {
            ambientLight.color = Color.Lerp(ambientLight.color, targetLightColor, Time.deltaTime * transitionSpeed);
            ambientLight.intensity = Mathf.Lerp(ambientLight.intensity, targetLightIntensity, Time.deltaTime * transitionSpeed);
        }

        if (bodyMaterialInstance != null)
        {
            Color current = bodyMaterialInstance.color;
            bodyMaterialInstance.color = Color.Lerp(current, targetBodyColor, Time.deltaTime * transitionSpeed);
        }
    }

    private void HandleStateChanged(TeacherState oldState, TeacherState newState)
    {
        switch (newState)
        {
            case TeacherState.Neutral:
                targetLightColor = neutralLightColor;
                targetBodyColor = neutralBodyColor;
                targetLightIntensity = neutralLightIntensity;
                break;
            case TeacherState.Pleased:
                targetLightColor = pleasedLightColor;
                targetBodyColor = pleasedBodyColor;
                targetLightIntensity = pleasedLightIntensity;
                break;
            case TeacherState.Angry:
                targetLightColor = angryLightColor;
                targetBodyColor = angryBodyColor;
                targetLightIntensity = angryLightIntensity;
                break;
        }
    }
}