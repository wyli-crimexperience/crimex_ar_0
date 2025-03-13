using UnityEngine;
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

    void Start()
    {
        if (!titleText || !descriptionText || !hudPanel)
        {
            Debug.LogError("Missing UI components in " + gameObject.name);
            return;
        }

        hudPanel.SetActive(false);

        if (TouchHandler.Instance)
            TouchHandler.Instance.onTouch.AddListener(HandleTouch);
        else
            Debug.LogError("TouchHandler is missing!");

        closeButton?.onClick.AddListener(() => hudPanel.SetActive(false));
    }

    void HandleTouch(Touch touch)
    {
        if (touch.phase != TouchPhase.Began) return;

        Ray ray = Camera.main.ScreenPointToRay(touch.position);
        float raycastRadius = 0.05f; // Adjust this for easier tapping

        if (Physics.SphereCast(ray, raycastRadius, out RaycastHit hit, Mathf.Infinity))
        {
            Debug.Log("SphereCast hit: " + hit.collider.gameObject.name);

            if (hit.collider.gameObject == gameObject) // Check if this is the right model part
            {
                ShowDescription();
            }
        }
    }



    void ShowDescription()
    {
        titleText.text = modelTitle;
        descriptionText.text = modelDescription;
        hudPanel.SetActive(true);
    }

    void OnDestroy()
    {
        if (TouchHandler.Instance)
            TouchHandler.Instance.onTouch.RemoveListener(HandleTouch);
    }
}
