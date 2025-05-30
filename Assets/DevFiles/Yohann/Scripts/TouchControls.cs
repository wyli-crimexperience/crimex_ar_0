using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using Vuforia;
using System.Collections;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class ARObjectGestureController : MonoBehaviour
{
    [Header("References")]
    public Camera arCamera;
    public ObserverBehaviour vuforiaTarget;

    [Header("Rotation Settings")]
    [Range(0.1f, 2f)]
    public float rotationSpeed = 0.2f;
    [Range(1f, 10f)]
    public float inertiaDamp = 5f;
    public bool invertRotationX = false;
    public bool invertRotationY = false;

    [Header("Scaling Settings")]
    [Range(1f, 20f)]
    public float scaleSpeed = 5f;
    [Range(0.01f, 1f)]
    public float minScale = 0.1f;
    [Range(1f, 10f)]
    public float maxScale = 2f;
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Double Tap Reset")]
    [Range(0.1f, 1f)]
    public float doubleTapThreshold = 0.3f;
    public float resetAnimationDuration = 0.5f;
    public AnimationCurve resetCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Touch Sensitivity")]
    public float touchDeadZone = 5f; // Minimum pixel movement to register as gesture
    public float pinchDeadZone = 10f; // Minimum distance change for pinch

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // Private fields
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private Vector2 inertiaVelocity;
    private bool isTracked = false;
    private bool isResetting = false;

    // Touch state management
    private struct TouchState
    {
        public float lastTapTime;
        public int tapCount;
        public float initialPinchDistance;
        public Vector3 pinchStartScale;
        public Vector2 lastTouchPosition;
        public bool isFirstMove;
    }

    private TouchState touchState;

    // Events for extensibility
    public System.Action OnTransformReset;
    public System.Action<float> OnScaleChanged;
    public System.Action<Vector3> OnRotationChanged;

    #region Unity Lifecycle

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        if (vuforiaTarget != null)
            vuforiaTarget.OnTargetStatusChanged += OnVuforiaTargetStatusChanged;

        // Store initial values
        initialRotation = transform.rotation;
        initialScale = transform.localScale;

        // Initialize touch state
        touchState = new TouchState
        {
            lastTapTime = 0f,
            tapCount = 0,
            initialPinchDistance = 0f,
            pinchStartScale = Vector3.zero,
            lastTouchPosition = Vector2.zero,
            isFirstMove = true
        };
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();

        if (vuforiaTarget != null)
            vuforiaTarget.OnTargetStatusChanged -= OnVuforiaTargetStatusChanged;
    }

    private void Update()
    {
        if (!CanProcessInput()) return;

        var touches = Touch.activeTouches;

        // Handle different touch states
        switch (touches.Count)
        {
            case 0:
                HandleNoTouch();
                break;
            case 1:
                HandleSingleTouch(touches[0]);
                break;
            case 2:
                HandlePinchGesture(touches[0], touches[1]);
                break;
            default:
                // Reset states for multi-touch beyond 2 fingers
                ResetTouchStates();
                break;
        }

        // Clean up touch states
        if (touches.Count < 2)
        {
            touchState.initialPinchDistance = 0f;
        }
    }

    #endregion

    #region Input Processing

    private bool CanProcessInput()
    {
        if (!isTracked || isResetting) return false;
        if (InteractionState.CurrentMode != InteractionMode.Transform) return false;

        // Check if touching UI elements
        if (EventSystem.current != null && Touch.activeTouches.Count > 0)
        {
            for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
                if (EventSystem.current.IsPointerOverGameObject(Touch.activeTouches[i].finger.index))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void HandleNoTouch()
    {
        // Apply inertia when no touch is active
        if (inertiaVelocity.magnitude > 0.01f)
        {
            ApplyInertiaRotation();
        }

        // Reset first move flag
        touchState.isFirstMove = true;
    }

    private void HandleSingleTouch(Touch touch)
    {
        switch (touch.phase)
        {
            case UnityEngine.InputSystem.TouchPhase.Began:
                touchState.lastTouchPosition = touch.screenPosition;
                touchState.isFirstMove = true;
                inertiaVelocity = Vector2.zero;
                break;

            case UnityEngine.InputSystem.TouchPhase.Moved:
                HandleRotationGesture(touch);
                break;

            case UnityEngine.InputSystem.TouchPhase.Ended:
            case UnityEngine.InputSystem.TouchPhase.Canceled:
                HandleTapGesture();
                break;
        }
    }

    private void HandleRotationGesture(Touch touch)
    {
        Vector2 currentPosition = touch.screenPosition;
        Vector2 delta = currentPosition - touchState.lastTouchPosition;

        // Apply dead zone to prevent jittery movements
        if (delta.magnitude < touchDeadZone && !touchState.isFirstMove)
        {
            return;
        }

        touchState.isFirstMove = false;
        touchState.lastTouchPosition = currentPosition;

        // Store velocity for inertia
        inertiaVelocity = delta * Time.deltaTime;

        // Apply rotation
        ApplyRotation(delta);
    }

    private void HandlePinchGesture(Touch touch1, Touch touch2)
    {
        float currentDistance = Vector2.Distance(touch1.screenPosition, touch2.screenPosition);

        if (touchState.initialPinchDistance == 0f)
        {
            // Initialize pinch
            touchState.initialPinchDistance = currentDistance;
            touchState.pinchStartScale = transform.localScale;
            inertiaVelocity = Vector2.zero; // Stop rotation inertia

            LogDebug($"[Pinch Start] Initial distance: {currentDistance:F1}");
        }
        else
        {
            // Apply scaling with dead zone
            float distanceChange = Mathf.Abs(currentDistance - touchState.initialPinchDistance);
            if (distanceChange > pinchDeadZone)
            {
                ApplyScaling(currentDistance);
            }
        }
    }

    private void HandleTapGesture()
    {
        float currentTime = Time.time;
        float timeSinceLastTap = currentTime - touchState.lastTapTime;

        if (timeSinceLastTap <= doubleTapThreshold)
        {
            touchState.tapCount++;
            if (touchState.tapCount >= 2)
            {
                TriggerReset();
                touchState.tapCount = 0;
            }
        }
        else
        {
            touchState.tapCount = 1;
        }

        touchState.lastTapTime = currentTime;
    }

    #endregion

    #region Gesture Application

    private void ApplyRotation(Vector2 delta)
    {
        float rotationMultiplier = rotationSpeed * Time.deltaTime * 100f;

        float rotX = delta.y * rotationMultiplier * (invertRotationX ? -1f : 1f);
        float rotY = -delta.x * rotationMultiplier * (invertRotationY ? -1f : 1f);

        // Apply rotations in world space for more intuitive control
        transform.Rotate(Vector3.up, rotY, Space.World);
        transform.Rotate(arCamera.transform.right, rotX, Space.World);

        LogDebug($"[Rotate] Δ({delta.x:F1},{delta.y:F1}) → X: {rotX:F1}°, Y: {rotY:F1}°");

        OnRotationChanged?.Invoke(transform.eulerAngles);
    }

    private void ApplyInertiaRotation()
    {
        float rotationMultiplier = rotationSpeed * Time.deltaTime;

        float rotX = inertiaVelocity.y * rotationMultiplier * (invertRotationX ? -1f : 1f);
        float rotY = -inertiaVelocity.x * rotationMultiplier * (invertRotationY ? -1f : 1f);

        transform.Rotate(Vector3.up, rotY, Space.World);
        transform.Rotate(arCamera.transform.right, rotX, Space.World);

        // Dampen inertia
        inertiaVelocity = Vector2.Lerp(inertiaVelocity, Vector2.zero, Time.deltaTime * inertiaDamp);

        LogDebug($"[Inertia] Velocity: {inertiaVelocity.magnitude:F2}");
    }

    private void ApplyScaling(float currentDistance)
    {
        float scaleFactor = currentDistance / touchState.initialPinchDistance;

        // Apply curve for more natural scaling feel
        scaleFactor = scaleCurve.Evaluate(Mathf.InverseLerp(0.5f, 2f, scaleFactor));
        scaleFactor = Mathf.Lerp(0.5f, 2f, scaleFactor);

        Vector3 targetScale = touchState.pinchStartScale * scaleFactor;
        float clampedScale = Mathf.Clamp(targetScale.x, minScale, maxScale);
        targetScale = Vector3.one * clampedScale;

        // Smooth scaling
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale,
                                          Time.deltaTime * scaleSpeed);

        LogDebug($"[Scale] Factor: {scaleFactor:F2} → {clampedScale:F2}");

        OnScaleChanged?.Invoke(clampedScale);
    }

    #endregion

    #region Reset and Utility

    public void TriggerReset()
    {
        if (!isResetting)
        {
            StartCoroutine(AnimatedReset());
        }
    }

    private IEnumerator AnimatedReset()
    {
        isResetting = true;

        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        LogDebug("[Reset] Starting animated reset");

        while (elapsed < resetAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / resetAnimationDuration;
            float curveValue = resetCurve.Evaluate(progress);

            transform.rotation = Quaternion.Lerp(startRotation, initialRotation, curveValue);
            transform.localScale = Vector3.Lerp(startScale, initialScale, curveValue);

            yield return null;
        }

        // Ensure exact final values
        transform.rotation = initialRotation;
        transform.localScale = initialScale;

        ResetTouchStates();
        isResetting = false;

        LogDebug("[Reset] Animation complete");
        OnTransformReset?.Invoke();
    }

    public void ResetTransform()
    {
        StopAllCoroutines();
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
        ResetTouchStates();
        isResetting = false;

        LogDebug("[Reset] Immediate reset");
        OnTransformReset?.Invoke();
    }

    private void ResetTouchStates()
    {
        inertiaVelocity = Vector2.zero;
        touchState.tapCount = 0;
        touchState.initialPinchDistance = 0f;
        touchState.isFirstMove = true;
    }

    #endregion

    #region Vuforia Integration

    private void OnVuforiaTargetStatusChanged(ObserverBehaviour target, TargetStatus status)
    {
        bool wasTracked = isTracked;
        isTracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;

        if (!wasTracked && isTracked)
        {
            // Target became tracked - reset touch states
            ResetTouchStates();
        }

        LogDebug($"[Vuforia] Status: {status.Status} | Tracked: {isTracked}");
    }

    #endregion

    #region Debug and Utility

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ARGesture] {message}");
        }
    }

    // Public methods for external control
    public void SetRotationSpeed(float speed) => rotationSpeed = Mathf.Clamp(speed, 0.1f, 2f);
    public void SetScaleSpeed(float speed) => scaleSpeed = Mathf.Clamp(speed, 1f, 20f);
    public void SetScaleLimits(float min, float max)
    {
        minScale = Mathf.Max(0.01f, min);
        maxScale = Mathf.Max(minScale, max);
    }

    // Property accessors
    public bool IsTracked => isTracked;
    public bool IsResetting => isResetting;
    public float CurrentScale => transform.localScale.x;

    #endregion
}