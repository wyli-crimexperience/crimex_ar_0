using System.Collections;
using UnityEngine;
using TMPro;

public class SmartKeyboardHandler : MonoBehaviour
{
    [SerializeField] private RectTransform uiContainer;
    [SerializeField] private TMP_InputField[] inputFields;
    [SerializeField] private float paddingOffset = 100f;
    [SerializeField] private float animationDuration = 0.3f;

    private Vector2 originalPosition;
    private bool keyboardActive = false;
    private Camera uiCamera;

    void Start()
    {
        originalPosition = uiContainer.anchoredPosition;

        Canvas canvas = uiContainer.GetComponentInParent<Canvas>();
        uiCamera = canvas.worldCamera ?? Camera.main;

        foreach (TMP_InputField field in inputFields)
        {
            field.onSelect.AddListener((string text) => OnInputFieldSelected(field));
            field.onDeselect.AddListener((string text) => OnInputFieldDeselected());
        }
    }

    void OnInputFieldSelected(TMP_InputField selectedField)
    {
        if (!keyboardActive)
        {
            StartCoroutine(HandleInputFieldSelection(selectedField));
        }
    }

    void OnInputFieldDeselected()
    {
        if (keyboardActive)
        {
            StartCoroutine(MoveUI(Vector2.zero));
            keyboardActive = false;
        }
    }

    IEnumerator HandleInputFieldSelection(TMP_InputField selectedField)
    {
        keyboardActive = true;

        // Wait for keyboard to appear
        yield return new WaitForSeconds(0.1f);

        float keyboardHeight = GetEstimatedKeyboardHeight();
        float inputFieldBottom = GetInputFieldBottomPosition(selectedField);
        float screenBottom = 0f;

        // Calculate how much the input field is obscured by the keyboard
        float obscuredAmount = (screenBottom + keyboardHeight) - inputFieldBottom;

        if (obscuredAmount > 0)
        {
            float moveDistance = obscuredAmount + paddingOffset;
            Debug.Log($"Input field obscured by {obscuredAmount}px, moving UI up by {moveDistance}px");
            yield return StartCoroutine(MoveUI(new Vector2(0, moveDistance)));
        }
        else
        {
            // Input field is not obscured, but move slightly for better UX
            yield return StartCoroutine(MoveUI(new Vector2(0, keyboardHeight * 0.3f)));
        }
    }

    float GetInputFieldBottomPosition(TMP_InputField inputField)
    {
        Vector3 worldPos = inputField.transform.position;
        Vector3 screenPos = uiCamera.WorldToScreenPoint(worldPos);

        // Account for input field height
        RectTransform rectTransform = inputField.GetComponent<RectTransform>();
        float fieldHeight = rectTransform.rect.height;

        return screenPos.y - (fieldHeight / 2f);
    }

    float GetEstimatedKeyboardHeight()
    {
        // Try to get actual keyboard height first
        if (TouchScreenKeyboard.visible && TouchScreenKeyboard.area.height > 0)
        {
            return TouchScreenKeyboard.area.height;
        }

        // Use platform-specific estimates
#if UNITY_ANDROID
        return Screen.height * 0.4f; // Android virtual keyboards
#elif UNITY_IOS
        return Screen.height * 0.35f; // iOS keyboards
#else
        return Screen.height * 0.35f; // Default estimate
#endif
    }

    IEnumerator MoveUI(Vector2 targetOffset)
    {
        Vector2 startPos = uiContainer.anchoredPosition;
        Vector2 targetPos = originalPosition + targetOffset;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            uiContainer.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        uiContainer.anchoredPosition = targetPos;
    }
}