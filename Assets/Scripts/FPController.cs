using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float mouseSensitivity = 0.1f;
    public float gravity = -20f;

    [Header("Running / Stamina")]
    [Tooltip("Moltiplicatore di velocità durante la corsa (tieni premuto Shift)")]
    public float runSpeedMultiplier = 2f;
    [Tooltip("Secondi di corsa continua per riempire la barra ed essere scoperti. Accovacciato la svuota nello stesso tempo.")]
    public float maxRunTime = 2f;
    [Tooltip("Quanto velocemente la barra sale camminando rispetto alla corsa (0.25 = un quarto)")]
    [Range(0f, 1f)] public float walkFillMultiplier = 0.25f;
    [Tooltip("Quanto lentamente la barra scende stando fermi in piedi, rispetto al crouch (0.15 = molto lenta)")]
    [Range(0f, 1f)] public float idleDrainMultiplier = 0.15f;
    [Tooltip("Moltiplicatore di velocità mentre sei accovacciato (Ctrl)")]
    public float crouchSpeedMultiplier = 0.5f;
    [Tooltip("Di quanto si abbassa la camera quando sei accovacciato (in metri)")]
    public float crouchCameraDrop = 0.6f;
    [Tooltip("Velocità di transizione dell'abbassamento/rialzo della camera")]
    public float crouchTransitionSpeed = 8f;
    [Tooltip("Immagine UI (Image Type = Filled) che mostra la barra di corsa")]
    public Image runBarFill;
    [Tooltip("Se true il player può correre")]
    public bool canRun = true;
    [Tooltip("Distanza dalla sedia a cui il player si posiziona quando si alza")]
    public float standOffset = 1.2f;
    [Tooltip("Distanza massima dalla sedia/seatPoint entro cui è possibile sedersi")]
    public float sitRange = 1.5f;

    [Header("References")]
    public Transform cameraTransform;
    public Transform seatPoint;
    public Transform standPoint;
    public Transform chairTransform;

    [Header("State")]
    public bool isSeated = true;

    [Header("External Lock")]
    [Tooltip("Se true, il player � forzato a restare seduto e non pu� alzarsi")]
    public bool forceSeated = false;

    [Tooltip("Se true, il player � completamente disabilitato (niente movimento, niente mouse)")]
    public bool gameplayFrozen = false;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;
    private bool eKeyWasPressed;
    private Vector3 chairOriginalPosition;
    public event Action OnPlayerStoodUp;
    public event Action OnPlayerSatDown;

    // Corsa
    private float runStamina = 0f; // 0..maxRunTime
    private bool runLimitTriggered = false;
    private float standCameraY; // altezza locale della camera da in piedi
    public bool IsRunning { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsMoving { get; private set; }
    public float RunStaminaNormalized => maxRunTime > 0f ? Mathf.Clamp01(runStamina / maxRunTime) : 0f;

    // True quando la barra di esposizione è al massimo (player sul punto di essere scoperto).
    public bool IsExposureMaxed => runStamina >= maxRunTime;

    // Scatta quando la barra di corsa si riempie del tutto: la prof ti scopre.
    public event Action OnRunLimitExceeded;

    // Aggiunge esposizione alla barra dall'esterno (es. furto in corso). Replica la
    // soglia di "scoperto" di UpdateRunStamina, riusando lo stesso guard runLimitTriggered
    // così l'evento non scatta due volte e si resetta quando la barra si svuota.
    public void AddExposure(float amount)
    {
        if (amount <= 0f) return;

        runStamina = Mathf.Clamp(runStamina + amount, 0f, maxRunTime);

        if (runBarFill != null)
            runBarFill.fillAmount = RunStaminaNormalized;

        if (runStamina >= maxRunTime && !runLimitTriggered)
        {
            runLimitTriggered = true;
            OnRunLimitExceeded?.Invoke();
        }
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (chairTransform != null)
            chairOriginalPosition = chairTransform.position;

        if (cameraTransform != null)
            standCameraY = cameraTransform.localPosition.y;

        if (seatPoint != null)
            TeleportTo(seatPoint);
    }

    void Update()
    {
        if (gameplayFrozen) return;

        HandleLook();

        // Safety: se siamo forzati seduti ma siamo in piedi, forza il sit down
        if (forceSeated && !isSeated)
        {
            SitDown();
        }

        HandleSitStandToggle();

        IsRunning = false;
        IsCrouching = false;
        IsMoving = false;
        if (!isSeated)
        {
            HandleMovement();
        }

        UpdateRunStamina();
        UpdateCrouchCamera();
    }

    void UpdateCrouchCamera()
    {
        if (cameraTransform == null) return;

        float targetY = IsCrouching ? standCameraY - crouchCameraDrop : standCameraY;
        Vector3 lp = cameraTransform.localPosition;
        lp.y = Mathf.Lerp(lp.y, targetY, crouchTransitionSpeed * Time.deltaTime);
        cameraTransform.localPosition = lp;
    }

    void UpdateRunStamina()
    {
        // Seduto o accovacciato (Ctrl) = la barra si svuota.
        // Corsa = riempimento pieno; camminata = riempimento a metà;
        // fermo in piedi = svuotamento lentissimo.
        float delta;
        if (isSeated || IsCrouching)
            delta = -Time.deltaTime;
        else if (IsRunning)
            delta = Time.deltaTime;
        else if (IsMoving)
            delta = Time.deltaTime * walkFillMultiplier;
        else
            delta = -Time.deltaTime * idleDrainMultiplier;

        runStamina += delta;
        runStamina = Mathf.Clamp(runStamina, 0f, maxRunTime);

        // Limite superato: la prof ti scopre (una sola volta finché non si svuota).
        if (runStamina >= maxRunTime && !runLimitTriggered)
        {
            runLimitTriggered = true;
            OnRunLimitExceeded?.Invoke();
        }
        if (runStamina <= 0f)
            runLimitTriggered = false;

        if (runBarFill != null)
            runBarFill.fillAmount = RunStaminaNormalized;
    }

    void HandleLook()
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleSitStandToggle()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool eIsPressed = kb.eKey.isPressed;

        // Detect "key just pressed" (rising edge)
        if (eIsPressed && !eKeyWasPressed)
        {
            if (forceSeated)
            {
                if (!isSeated) SitDown();
            }
            else
            {
                if (isSeated)
                    StandUp();
                else if (CanSitDown())
                    SitDown();
            }
        }

        eKeyWasPressed = eIsPressed;
    }

    bool CanSitDown()
    {
        Vector3 playerFlat = new Vector3(transform.position.x, 0f, transform.position.z);

        // Condizione 1: vicino al seatPoint
        if (seatPoint != null)
        {
            Vector3 seatFlat = new Vector3(seatPoint.position.x, 0f, seatPoint.position.z);
            if (Vector3.Distance(playerFlat, seatFlat) <= sitRange)
                return true;
        }

        // Condizione 2: guardando la sedia da abbastanza vicino
        if (chairTransform != null)
        {
            Vector3 toChair = chairTransform.position - transform.position;
            toChair.y = 0f;
            if (toChair.magnitude <= sitRange)
            {
                Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
                if (Vector3.Dot(forwardFlat, toChair.normalized) >= 0.5f)
                    return true;
            }
        }

        return false;
    }

    void StandUp()
    {
        if (standPoint == null || seatPoint == null) return;

        // Il player si alza sul posto (stessa XZ della sedia, altezza da standPoint)
        Vector3 standPos = seatPoint.position;
        standPos.y = standPoint.position.y;

        controller.enabled = false;
        transform.position = standPos;
        controller.enabled = true;
        verticalVelocity = 0f;

        // La sedia si sposta sempre lontano dal banco (direzione seatPoint → standPoint)
        if (chairTransform != null)
        {
            Vector3 awayFromDesk = new Vector3(
                standPoint.position.x - seatPoint.position.x,
                0f,
                standPoint.position.z - seatPoint.position.z).normalized;
            chairTransform.position = chairOriginalPosition + awayFromDesk * standOffset;
        }

        isSeated = false;
        Debug.Log("Player si� alzato");
        OnPlayerStoodUp?.Invoke();
    }

    void SitDown()
    {
        if (seatPoint == null) return;
        TeleportTo(seatPoint);

        if (chairTransform != null)
            chairTransform.position = chairOriginalPosition;

        isSeated = true;
        Debug.Log("Player si � seduto");
        OnPlayerSatDown?.Invoke();
    }

    void TeleportTo(Transform target)
    {
        controller.enabled = false;
        transform.position = target.position;
        controller.enabled = true;
        verticalVelocity = 0f;
    }

    void HandleMovement()
    {
        Vector2 input = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
        }

        Vector3 move = (transform.right * input.x + transform.forward * input.y).normalized;

        bool isMoving = input.sqrMagnitude > 0.01f;
        IsMoving = isMoving;
        bool crouching = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
        IsCrouching = crouching;

        // Accovacciato non si può correre.
        bool wantsToRun = canRun && isMoving && !crouching && kb != null &&
                          (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        IsRunning = wantsToRun;

        float currentSpeed = moveSpeed;
        if (crouching) currentSpeed *= crouchSpeedMultiplier;
        else if (wantsToRun) currentSpeed *= runSpeedMultiplier;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * currentSpeed * Time.deltaTime);
    }
}