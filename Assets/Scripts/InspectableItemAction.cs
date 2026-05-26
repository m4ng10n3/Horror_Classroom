using UnityEngine;
using UnityEngine.InputSystem;

public class InspectableItemAction : MonoBehaviour
{
    [Header("Input")]
    public Key actionKey = Key.Space;
    public string actionLabel = "Apri/chiudi";

    [Header("Transform Toggle")]
    public bool animateTransform = true;
    public Transform targetTransform;
    public bool useCurrentPoseAsClosed = true;
    public bool animatePosition = false;
    public bool animateRotation = true;
    public Vector3 closedLocalPosition;
    public Vector3 openLocalPosition;
    public Vector3 closedLocalEulerAngles;
    public Vector3 openLocalEulerAngles;
    [Min(0f)]
    public float transitionDuration = 0.25f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Animator opzionale")]
    public Animator animator;
    public bool useAnimatorBool = false;
    public string openBoolParameter = "IsOpen";
    public bool useAnimatorTriggers = false;
    public string openTriggerParameter = "Open";
    public string closeTriggerParameter = "Close";

    private bool isOpen;
    private float transitionTime;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Quaternion startRotation;
    private Quaternion targetRotation;

    public string HintText => $"[{actionKey}] {actionLabel}";

    public void InitializeForInspection()
    {
        if (targetTransform == null)
            targetTransform = transform;
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (useCurrentPoseAsClosed && targetTransform != null)
        {
            closedLocalPosition = targetTransform.localPosition;
            closedLocalEulerAngles = targetTransform.localEulerAngles;
        }

        isOpen = false;
        transitionTime = transitionDuration;
        ApplyPose(0f);
        ApplyAnimatorState();
    }

    public bool WasActionPressed(Keyboard keyboard)
    {
        if (keyboard == null || actionKey == Key.None)
            return false;

        var key = keyboard[actionKey];
        return key != null && key.wasPressedThisFrame;
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        BeginTransformTransition();
        ApplyAnimatorState();
    }

    public void Tick(float deltaTime)
    {
        if (!animateTransform || targetTransform == null || transitionTime >= transitionDuration)
            return;

        transitionTime += deltaTime;
        float t = transitionDuration <= 0f ? 1f : Mathf.Clamp01(transitionTime / transitionDuration);
        float eased = transitionCurve != null ? transitionCurve.Evaluate(t) : t;

        if (animatePosition)
            targetTransform.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
        if (animateRotation)
            targetTransform.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);
    }

    private void BeginTransformTransition()
    {
        if (!animateTransform || targetTransform == null)
            return;

        transitionTime = 0f;
        startPosition = targetTransform.localPosition;
        startRotation = targetTransform.localRotation;
        targetPosition = isOpen ? openLocalPosition : closedLocalPosition;
        targetRotation = Quaternion.Euler(isOpen ? openLocalEulerAngles : closedLocalEulerAngles);

        if (transitionDuration <= 0f)
            ApplyPose(isOpen ? 1f : 0f);
    }

    private void ApplyPose(float openAmount)
    {
        if (!animateTransform || targetTransform == null)
            return;

        if (animatePosition)
            targetTransform.localPosition = Vector3.LerpUnclamped(closedLocalPosition, openLocalPosition, openAmount);
        if (animateRotation)
            targetTransform.localRotation = Quaternion.SlerpUnclamped(
                Quaternion.Euler(closedLocalEulerAngles),
                Quaternion.Euler(openLocalEulerAngles),
                openAmount);
    }

    private void ApplyAnimatorState()
    {
        if (animator == null)
            return;

        if (useAnimatorBool && HasAnimatorParameter(openBoolParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(openBoolParameter, isOpen);

        if (!useAnimatorTriggers)
            return;

        string trigger = isOpen ? openTriggerParameter : closeTriggerParameter;
        if (HasAnimatorParameter(trigger, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(trigger);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
