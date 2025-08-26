using UnityEngine;

public class SpriteRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 90f; // Degrees per second
    [SerializeField] private bool rotateClockwise = true;

    [Header("Optional Settings")]
    [SerializeField] private bool useUnscaledTime = false; // Ignore time scale
    [SerializeField] private float maxRotationAngle = 0f; // 0 = unlimited

    private bool isRotating = false;
    private float currentRotation = 0f;

    private void OnEnable()
    {
        StartRotation();
    }

    private void OnDisable()
    {
        StopRotation();
    }

    void Update()
    {
        if (isRotating)
        {
            RotateSprite();
        }
    }

    private void RotateSprite()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float rotationAmount = rotationSpeed * deltaTime;

        if (!rotateClockwise)
        {
            rotationAmount = -rotationAmount;
        }

        // Apply rotation
        transform.Rotate(0, 0, rotationAmount);
        currentRotation += Mathf.Abs(rotationAmount);

        // Check if we've reached max rotation
        if (maxRotationAngle > 0 && currentRotation >= maxRotationAngle)
        {
            StopRotation();
        }
    }

    public void StartRotation()
    {
        isRotating = true;
        currentRotation = 0f;
    }

    public void StopRotation()
    {
        isRotating = false;
    }

    // Reset rotation to zero
    public void ResetRotation()
    {
        transform.rotation = Quaternion.identity;
        currentRotation = 0f;
    }
}
