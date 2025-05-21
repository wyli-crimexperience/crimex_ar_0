using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems;
using Vuforia;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class ARObjectGestureController : MonoBehaviour
{
    [Header("References")]
    public Camera arCamera;
    public ObserverBehaviour vuforiaTarget;

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.2f;
    public float inertiaDamp = 5f;

    [Header("Scaling Settings")]
    public float scaleSpeed = 5f;
    public float minScale = 0.1f;
    public float maxScale = 2f;

    [Header("Double Tap Reset")]
    public float doubleTapThreshold = 0.3f;

    private Quaternion initialRotation;
    private Vector3 initialScale;

    private Vector2 inertiaVelocity;
    private float lastTapTime;
    private int tapCount;
    private float initialPinchDistance;
    private Vector3 pinchStartScale;

    private bool isTracked = false;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        if (vuforiaTarget)
            vuforiaTarget.OnTargetStatusChanged += OnVuforiaTargetStatusChanged;

        initialRotation = transform.rotation;
        initialScale = transform.localScale;
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();

        if (vuforiaTarget)
            vuforiaTarget.OnTargetStatusChanged -= OnVuforiaTargetStatusChanged;
    }

    private void OnVuforiaTargetStatusChanged(ObserverBehaviour target, TargetStatus status)
    {
        isTracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;
        Debug.Log($"[Vuforia] Tracking state: {status.Status}");
    }

    private void Update()
    {
        if (!isTracked) return;
        if (InteractionState.CurrentMode != InteractionMode.Transform) return;

        var touches = Touch.activeTouches;

        // Skip if touching UI
        if (EventSystem.current != null && touches.Count > 0 && EventSystem.current.IsPointerOverGameObject(0))
        {
            return;
        }

        // Inertia (when no touch)
        if (touches.Count == 0 && inertiaVelocity.magnitude > 0.01f)
        {
            float rotX = inertiaVelocity.y * rotationSpeed * Time.deltaTime;
            float rotY = -inertiaVelocity.x * rotationSpeed * Time.deltaTime;

            transform.Rotate(Vector3.up, rotY, Space.World);
            transform.Rotate(Vector3.right, rotX, Space.World);

            Debug.Log($"[Inertia] Rotating with velocity {inertiaVelocity}");

            inertiaVelocity = Vector2.Lerp(inertiaVelocity, Vector2.zero, Time.deltaTime * inertiaDamp);
        }

        // Single finger rotation
        if (touches.Count == 1)
        {
            Touch touch = touches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                float timeSinceLastTap = Time.time - lastTapTime;

                if (timeSinceLastTap <= doubleTapThreshold)
                {
                    tapCount++;
                    if (tapCount == 2)
                    {
                        ResetTransform();
                        Debug.Log("[Double Tap] Transform reset.");
                        tapCount = 0;

                        // Optional haptic feedback
                        // Handheld.Vibrate();
                    }
                }
                else
                {
                    tapCount = 1;
                }

                lastTapTime = Time.time;
            }

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 delta = touch.delta;
                inertiaVelocity = delta;

                float rotX = delta.y * rotationSpeed * Time.deltaTime * 100f;
                float rotY = -delta.x * rotationSpeed * Time.deltaTime * 100f;

                transform.Rotate(Vector3.up, rotY, Space.World);
                transform.Rotate(Vector3.right, rotX, Space.World);

                Debug.Log($"[Rotate] Δ({delta.x:F2},{delta.y:F2}) → rotX: {rotX:F1}, rotY: {rotY:F1}");
            }
        }

        // Pinch to scale
        if (touches.Count == 2)
        {
            var t0 = touches[0];
            var t1 = touches[1];

            float currentDistance = Vector2.Distance(t0.screenPosition, t1.screenPosition);

            if (initialPinchDistance == 0)
            {
                initialPinchDistance = currentDistance;
                pinchStartScale = transform.localScale;
                Debug.Log($"[Pinch Start] Distance: {initialPinchDistance}");
            }
            else
            {
                float scaleFactor = currentDistance / initialPinchDistance;
                Vector3 targetScale = pinchStartScale * scaleFactor;

                float clamped = Mathf.Clamp(targetScale.x, minScale, maxScale);
                targetScale = Vector3.one * clamped;

                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
                Debug.Log($"[Pinch Scale] Factor: {scaleFactor:F2}, New Scale: {transform.localScale}");
            }

            inertiaVelocity = Vector2.zero;
        }

        // Reset pinch state when fewer than 2 fingers are down
        if (touches.Count < 2)
        {
            initialPinchDistance = 0;
        }
    }

    public void ResetTransform()
    {
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
        inertiaVelocity = Vector2.zero;
        tapCount = 0;

        Debug.Log("[Reset] Transform returned to initial state.");
    }
}
