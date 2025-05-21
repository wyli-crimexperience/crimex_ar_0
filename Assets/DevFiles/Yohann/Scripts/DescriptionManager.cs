using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class DescriptionManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public GameObject hudPanel;
    public Button closeButton;

    [Header("Model Information")]
    [SerializeField] private string modelTitle = "Model Part";
    [SerializeField] private string modelDescription = "Description of the model part";

    private void Start()
    {
        if (!titleText || !descriptionText || !hudPanel)
        {
            Debug.LogError("Missing UI components in " + gameObject.name);
            return;
        }

        hudPanel.SetActive(false);
        closeButton?.onClick.AddListener(() => hudPanel.SetActive(false));
    }

    private void Update()
    {
        if (InteractionState.CurrentMode != InteractionMode.Description) return;

        if (Touchscreen.current == null || Touchscreen.current.primaryTouch == null)
            return;

        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            Vector2 touchPos = touch.position.ReadValue();
            HandleTouch(touchPos);
        }
    }

    private void HandleTouch(Vector2 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        float raycastRadius = 0.05f;

        if (Physics.SphereCast(ray, raycastRadius, out RaycastHit hit, Mathf.Infinity))
        {
            Debug.Log("SphereCast hit: " + hit.collider.gameObject.name);

            if (hit.collider.gameObject == gameObject)
            {
                ShowDescription();
            }
        }
    }

    private void ShowDescription()
    {
        titleText.text = modelTitle;
        descriptionText.text = modelDescription;
        hudPanel.SetActive(true);
        Debug.Log($"[DescriptionManager] Showing description for {modelTitle}");
    }
}
