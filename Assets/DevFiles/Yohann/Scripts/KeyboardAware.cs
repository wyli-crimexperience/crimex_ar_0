using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SmartKeyboardHandler : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform uiContainer;
    [SerializeField] private TMP_InputField[] inputFields;

    [Header("Configuration")]
    [SerializeField] private float paddingOffset = 100f;
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float keyboardDetectionDelay = 0.15f;
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Platform Overrides")]
    [SerializeField] private float androidKeyboardRatio = 0.4f;
    [SerializeField] private float iosKeyboardRatio = 0.35f;

    private Vector2 originalPosition;
    private bool keyboardActive = false;
    private bool isMoving = false;
    private Camera uiCamera;
    private Coroutine currentMoveCoroutine;
    private TMP_InputField lastSelectedField;
    private Canvas parentCanvas;

    // Event handlers for external systems
    public System.Action<bool> OnKeyboardStateChanged;
    public System.Action<float> OnUIPositionChanged;

    void Start()
    {
        InitializeHandler();
        RegisterInputFieldEvents();
    }

    void OnDestroy()
    {
        UnregisterInputFieldEvents();
        if (currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
        }
    }

    private void InitializeHandler()
    {
        originalPosition = uiContainer.anchoredPosition;
        parentCanvas = uiContainer.GetComponentInParent<Canvas>();

        // Better camera detection
        if (parentCanvas != null)
        {
            uiCamera = parentCanvas.worldCamera;
            if (uiCamera == null && parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                uiCamera = Camera.main;
            }
        }
        else
        {
            uiCamera = Camera.main;
        }

        if (enableDebugLogs)
            Debug.Log($"SmartKeyboardHandler initialized with camera: {uiCamera?.name ?? "None"}");
    }

    private void RegisterInputFieldEvents()
    {
        foreach (TMP_InputField field in inputFields)
        {
            if (field != null)
            {
                field.onSelect.AddListener((string text) => OnInputFieldSelected(field));
                field.onDeselect.AddListener((string text) => OnInputFieldDeselected(field));
                field.onEndEdit.AddListener((string text) => OnInputFieldEndEdit(field));
            }
        }
    }

    private void UnregisterInputFieldEvents()
    {
        foreach (TMP_InputField field in inputFields)
        {
            if (field != null)
            {
                field.onSelect.RemoveAllListeners();
                field.onDeselect.RemoveAllListeners();
                field.onEndEdit.RemoveAllListeners();
            }
        }
    }

    void OnInputFieldSelected(TMP_InputField selectedField)
    {
        if (selectedField == null || isMoving) return;

        lastSelectedField = selectedField;

        if (!keyboardActive)
        {
            if (currentMoveCoroutine != null)
                StopCoroutine(currentMoveCoroutine);

            currentMoveCoroutine = StartCoroutine(HandleInputFieldSelection(selectedField));
        }
        else
        {
            // Keyboard already active, just adjust position for new field
            if (currentMoveCoroutine != null)
                StopCoroutine(currentMoveCoroutine);

            currentMoveCoroutine = StartCoroutine(AdjustForNewField(selectedField));
        }
    }

    void OnInputFieldDeselected(TMP_InputField deselectedField)
    {
        // Only hide keyboard if no other input field is selected
        StartCoroutine(CheckKeyboardHide());
    }

    void OnInputFieldEndEdit(TMP_InputField field)
    {
        // Additional safety check for hiding keyboard
        if (field == lastSelectedField)
        {
            StartCoroutine(CheckKeyboardHide());
        }
    }

    IEnumerator CheckKeyboardHide()
    {
        yield return new WaitForEndOfFrame();

        // Check if any input field is still active
        bool anyFieldActive = false;
        foreach (var field in inputFields)
        {
            if (field != null && field.isFocused)
            {
                anyFieldActive = true;
                break;
            }
        }

        if (!anyFieldActive && keyboardActive)
        {
            if (currentMoveCoroutine != null)
                StopCoroutine(currentMoveCoroutine);

            currentMoveCoroutine = StartCoroutine(HideKeyboard());
        }
    }

    IEnumerator HandleInputFieldSelection(TMP_InputField selectedField)
    {
        keyboardActive = true;
        OnKeyboardStateChanged?.Invoke(true);

        if (enableDebugLogs)
            Debug.Log("Keyboard activation detected");

        // Wait for keyboard to appear with better detection
        yield return new WaitForSeconds(keyboardDetectionDelay);

        // Double-check keyboard visibility
        float waitTime = 0f;
        while (!IsKeyboardVisible() && waitTime < 1f)
        {
            yield return new WaitForSeconds(0.05f);
            waitTime += 0.05f;
        }

        yield return CalculateAndMoveUI(selectedField);
    }

    IEnumerator AdjustForNewField(TMP_InputField selectedField)
    {
        if (enableDebugLogs)
            Debug.Log("Adjusting UI for new input field");

        yield return CalculateAndMoveUI(selectedField);
    }

    IEnumerator CalculateAndMoveUI(TMP_InputField selectedField)
    {
        float keyboardHeight = GetKeyboardHeight();
        float inputFieldBottom = GetInputFieldBottomPosition(selectedField);

        if (enableDebugLogs)
            Debug.Log($"Keyboard height: {keyboardHeight}, Input field bottom: {inputFieldBottom}");

        // Calculate how much the input field is obscured
        float obscuredAmount = keyboardHeight - inputFieldBottom;

        Vector2 targetOffset;
        if (obscuredAmount > 0)
        {
            float moveDistance = obscuredAmount + paddingOffset;
            targetOffset = new Vector2(0, moveDistance);

            if (enableDebugLogs)
                Debug.Log($"Input field obscured by {obscuredAmount}px, moving UI up by {moveDistance}px");
        }
        else
        {
            // Input field is visible, but provide some padding for better UX
            float minPadding = keyboardHeight * 0.1f;
            targetOffset = new Vector2(0, minPadding);

            if (enableDebugLogs)
                Debug.Log($"Input field visible, applying minimal padding: {minPadding}px");
        }

        yield return StartCoroutine(MoveUI(targetOffset));
    }

    IEnumerator HideKeyboard()
    {
        keyboardActive = false;
        OnKeyboardStateChanged?.Invoke(false);

        if (enableDebugLogs)
            Debug.Log("Hiding keyboard, restoring UI position");

        yield return StartCoroutine(MoveUI(Vector2.zero));
    }

    private bool IsKeyboardVisible()
    {
        return TouchScreenKeyboard.visible;
    }

    float GetInputFieldBottomPosition(TMP_InputField inputField)
    {
        if (inputField == null) return 0f;

        RectTransform rectTransform = inputField.GetComponent<RectTransform>();

        if (parentCanvas?.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // For screen space overlay, use direct screen coordinates
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return corners[0].y; // Bottom-left corner
        }
        else
        {
            // For screen space camera or world space
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector3 screenPos = uiCamera.WorldToScreenPoint(corners[0]);
            return screenPos.y;
        }
    }

    float GetKeyboardHeight()
    {
        // Try to get actual keyboard height first
        if (IsKeyboardVisible() && TouchScreenKeyboard.area.height > 0)
        {
            return TouchScreenKeyboard.area.height;
        }

        // Fallback to platform-specific estimates
#if UNITY_ANDROID
        return Screen.height * androidKeyboardRatio;
#elif UNITY_IOS
        return Screen.height * iosKeyboardRatio;
#else
        return Screen.height * 0.35f;
#endif
    }

    IEnumerator MoveUI(Vector2 targetOffset)
    {
        isMoving = true;

        Vector2 startPos = uiContainer.anchoredPosition;
        Vector2 targetPos = originalPosition + targetOffset;

        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time for UI
            float t = animationCurve.Evaluate(elapsed / animationDuration);

            Vector2 currentPos = Vector2.Lerp(startPos, targetPos, t);
            uiContainer.anchoredPosition = currentPos;

            OnUIPositionChanged?.Invoke(currentPos.y - originalPosition.y);

            yield return null;
        }

        uiContainer.anchoredPosition = targetPos;
        OnUIPositionChanged?.Invoke(targetPos.y - originalPosition.y);

        isMoving = false;
        currentMoveCoroutine = null;
    }

    // Public methods for external control
    public void ForceHideKeyboard()
    {
        if (keyboardActive)
        {
            if (currentMoveCoroutine != null)
                StopCoroutine(currentMoveCoroutine);

            currentMoveCoroutine = StartCoroutine(HideKeyboard());
        }
    }

    public void RefreshInputFields()
    {
        UnregisterInputFieldEvents();
        inputFields = Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
        RegisterInputFieldEvents();
    }

    public bool IsKeyboardCurrentlyActive => keyboardActive;
    public Vector2 CurrentUIOffset => uiContainer.anchoredPosition - originalPosition;
}