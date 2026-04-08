using UnityEngine;

public class TeacherController : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Velocità di rotazione della prof in gradi/secondo")]
    public float rotationSpeed = 180f;

    // Stato target
    private bool isFacingClass = true;
    private float targetYRotation;

    void Start()
    {
        // Parte rivolta verso la classe (Z negativo)
        targetYRotation = 180f;
        transform.rotation = Quaternion.Euler(0f, targetYRotation, 0f);
    }

    void Update()
    {
        // Rotazione fluida verso il target
        Quaternion targetRot = Quaternion.Euler(0f, targetYRotation, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            rotationSpeed * Time.deltaTime
        );
    }

    public void FaceBoard()
    {
        isFacingClass = false;
        targetYRotation = 0f;
    }

    public void FaceClass()
    {
        isFacingClass = true;
        targetYRotation = 180f;
    }

    public bool IsFacingClass()
    {
        // Considera "girata verso la classe" solo quando ha quasi completato la rotazione
        float diff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, 180f));
        return isFacingClass && diff < 10f;
    }

    public bool IsFacingBoard()
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, 0f));
        return !isFacingClass && diff < 10f;
    }
}