using UnityEngine;

public class FootstepSystem : MonoBehaviour
{
    public CharacterController controller;

    [Tooltip("Controller del player: serve a capire se il giocatore VUOLE muoversi (IsMoving).")]
    public FPSController player;

    public AudioSource source;

    public AudioClip[] steps;

    [Tooltip("Distanza (in metri) tra un passo e l'altro. Più piccola = passi più fitti.")]
    public float stepDistance = 1.8f;

    [Range(0f, 0.5f)]
    [Tooltip("Variazione casuale della distanza tra un passo e l'altro (0.15 = ±15%). " +
             "Rompe il ritmo troppo regolare.")]
    public float stepDistanceVariation = 0.15f;

    [Tooltip("Velocità orizzontale minima per considerare il player in movimento.")]
    public float minSpeed = 0.2f;

    [Tooltip("Range di pitch casuale per ogni passo. Più ampio = suono meno ripetitivo.")]
    public Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    [Tooltip("Range di volume casuale per ogni passo.")]
    public Vector2 volumeRange = new Vector2(0.85f, 1f);

    float distanceSinceLastStep;
    float nextStepDistance;
    int lastStepIndex = -1;
    bool wasMoving;

    void Awake()
    {
        // Aggancio automatico se non assegnati nell'Inspector.
        if (controller == null) controller = GetComponentInParent<CharacterController>();
        if (player == null) player = GetComponentInParent<FPSController>();

        nextStepDistance = stepDistance;
    }

    void Update()
    {
        if (controller == null) return;

        // Velocità solo orizzontale: la gravità (Y) non deve contare come "passo".
        Vector3 horizontalVelocity = controller.velocity;
        horizontalVelocity.y = 0f;
        float speed = horizontalVelocity.magnitude;

        // Il player si muove davvero solo se: sta dando input di movimento,
        // ha velocità orizzontale sopra soglia ed è a terra.
        bool wantsToMove = player != null && player.IsMoving;
        bool isMoving = wantsToMove && speed > minSpeed && controller.isGrounded;

        if (isMoving)
        {
            if (!wasMoving)
            {
                // Appena partito: passo subito, così non si sente "in ritardo".
                PlayStep();
                distanceSinceLastStep = 0f;
            }
            else
            {
                // I passi seguono la distanza realmente percorsa: il ritmo si adatta
                // da solo alla velocità (camminata = passi radi, corsa = passi fitti).
                distanceSinceLastStep += speed * Time.deltaTime;

                if (distanceSinceLastStep >= nextStepDistance)
                {
                    PlayStep();
                    distanceSinceLastStep = 0f;
                }
            }
        }
        else
        {
            // Fermo: azzero, così non parte un passo "fantasma" al momento dello stop.
            distanceSinceLastStep = 0f;
        }

        wasMoving = isMoving;
    }

    void PlayStep()
    {
        if (steps == null || steps.Length == 0 || source == null) return;

        // Scelgo una clip diversa dall'ultima suonata: evita di risentire
        // due volte di fila lo stesso campione (effetto "tamburo").
        int index = Random.Range(0, steps.Length);
        if (steps.Length > 1 && index == lastStepIndex)
            index = (index + 1) % steps.Length;
        lastStepIndex = index;

        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(steps[index], Random.Range(volumeRange.x, volumeRange.y));

        // Prossimo passo a distanza leggermente variata: ritmo meno robotico.
        nextStepDistance = stepDistance * Random.Range(1f - stepDistanceVariation, 1f + stepDistanceVariation);
    }
}
