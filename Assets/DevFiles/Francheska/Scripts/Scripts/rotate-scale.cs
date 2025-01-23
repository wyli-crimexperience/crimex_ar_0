using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateObjectScript : MonoBehaviour
{
    public float pcRotationSpeed = 10f;
    public float mobileRotationSpeed = 0.4f;

    public Camera cam;

    private float initialDistance;
    private Vector3 initialScale;

    private void Update()
    {
        foreach (Touch touch in Input.touches)
        {
            Debug.Log("Touching at" + touch.position);
            Ray camRay = cam.ScreenPointToRay(touch.position);
            RaycastHit raycastHit;
            if (Physics.Raycast(camRay, out raycastHit, 10))
            {
                if (touch.phase == TouchPhase.Began)
                {
                    Debug.Log("Touch phase began at " + touch.position);
                }
                else if (touch.phase == TouchPhase.Moved)
                {
                    Debug.Log("Touch phase moved");
                    transform.Rotate(touch.deltaPosition.y * mobileRotationSpeed, -touch.deltaPosition.x * mobileRotationSpeed, 0, Space.World);
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    Debug.Log("Touch phase ended");
                }
            }
        }

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
                Debug.Log("Initial Distance: " + initialDistance);
            }
            else
            {
                var currentDistance = Vector2.Distance(touchZero.position, touchOne.position);

                if (Mathf.Approximately(initialDistance, 0))
                {
                    return;
                }

                var factor = currentDistance / initialDistance;
                transform.localScale = initialScale * factor;
            }
        }
    }
}
