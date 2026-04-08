using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("External Lock")]
    [Tooltip("Se true, il player è forzato a restare seduto e non può alzarsi")]
    public bool forceSeated = false;

    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float mouseSensitivity = 0.1f;
    public float gravity = -20f;

    [Header("References")]
    public Transform cameraTransform;
    public Transform seatPoint;
    public Transform standPoint;

    [Header("State")]
    public bool isSeated = true;

    private CharacterController controller;
    private float verticalVelocity;
    private float pitch;
    private bool eKeyWasPressed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Posizioniamo il player allo SeatPoint all'avvio
        if (seatPoint != null)
        {
            TeleportTo(seatPoint);
        }
    }

    void Update()
    {
        HandleLook();

        // Safety: se siamo forzati seduti ma siamo in piedi, forza il sit down
        if (forceSeated && !isSeated)
        {
            SitDown();
        }

        HandleSitStandToggle();

        if (!isSeated)
        {
            HandleMovement();
        }
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
                // Se siamo forzati a restare seduti, ignora il tasto E
                // (opzionale: assicurati di essere effettivamente seduto)
                if (!isSeated) SitDown();
            }
            else
            {
                if (isSeated)
                    StandUp();
                else
                    SitDown();
            }
        }

        eKeyWasPressed = eIsPressed;
    }

    void StandUp()
    {
        if (standPoint == null) return;
        TeleportTo(standPoint);
        isSeated = false;
        Debug.Log("Player si è alzato");
    }

    void SitDown()
    {
        if (seatPoint == null) return;
        TeleportTo(seatPoint);
        isSeated = true;
        Debug.Log("Player si è seduto");
    }

    void TeleportTo(Transform target)
    {
        // CharacterController va disabilitato temporaneamente per teletrasportare
        controller.enabled = false;
        transform.position = target.position;
        // Manteniamo la rotation Y attuale del player così non perde l'orientamento
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

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * moveSpeed * Time.deltaTime);
    }
}