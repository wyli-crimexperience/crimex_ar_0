using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateObjectScript : MonoBehaviour
{
    public float rotationSpeed = 0.2f; // Adjusted for smoother rotation
    public float scaleSpeed = 0.01f; // Added scale speed control

    public Camera cam;

    private float initialDistance;
    private Vector3 initialScale;
    private Quaternion initialRotation;
    private Vector2 lastTouchDelta; // Store last touch delta for smoothing

    private void Start()
    {
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
    }

    private void Update()
    {
        // Single touch for rotation
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            Ray camRay = cam.ScreenPointToRay(touch.position);
            RaycastHit raycastHit;

            if (Physics.Raycast(camRay, out raycastHit, 10))
            {
                if (touch.phase == TouchPhase.Moved)
                {
                    // Smooth rotation using Lerp
                    Vector2 touchDelta = touch.deltaPosition;
                    lastTouchDelta = Vector2.Lerp(lastTouchDelta, touchDelta, 0.1f); // Smooth out sudden movements

                    float rotationX = lastTouchDelta.y * rotationSpeed * Time.deltaTime * 100f;
                    float rotationY = -lastTouchDelta.x * rotationSpeed * Time.deltaTime * 100f;

                    transform.Rotate(rotationX, rotationY, 0, Space.World);
                }
            }
        }

        // Two-finger pinch for scaling
        if (Input.touchCount == 2)
        {
            var touchZero = Input.GetTouch(0);
            var touchOne = Input.GetTouch(1);

            if (touchZero.phase == TouchPhase.Ended || touchZero.phase == TouchPhase.Canceled ||
                touchOne.phase == TouchPhase.Ended || touchOne.phase == TouchPhase.Canceled)
            {
                return;
            }

            if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
            {
                initialDistance = Vector2.Distance(touchZero.position, touchOne.position);
                initialScale = transform.localScale;
            }
            else
            {
                var currentDistance = Vector2.Distance(touchZero.position, touchOne.position);

                if (Mathf.Approximately(initialDistance, 0))
                {
                    return;
                }

                var factor = currentDistance / initialDistance;
                transform.localScale = Vector3.Lerp(transform.localScale, initialScale * factor, scaleSpeed);
            }
        }
    }

    public void ResetTransform()
    {
        transform.rotation = initialRotation;
        transform.localScale = initialScale;
    }
}
