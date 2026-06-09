using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float mouseSensitivity = 0.1f;
    public float gravity = -20f;
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

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * moveSpeed * Time.deltaTime);
    }
}