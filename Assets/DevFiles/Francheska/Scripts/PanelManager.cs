using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class PanelManager : MonoBehaviour
{
    [System.Serializable]
    public class PanelData
    {
        public string panelName;
        public GameObject panelObject;
        public Button backButton;
    }

    [SerializeField] private List<PanelData> panels = new List<PanelData>();
    private Dictionary<string, PanelData> panelDict;
    private string currentPanelName;

    private void Awake()
    {
        // Create dictionary for quick lookup
        panelDict = panels.ToDictionary(p => p.panelName);

        // Hide all panels except home
        foreach (var panel in panels)
        {
            if (panel.panelName != "Home")
                panel.panelObject.SetActive(false);
            else
            {
                panel.panelObject.SetActive(true);
                currentPanelName = panel.panelName;
            }
        }
    }

    public void SwitchToPanel(string targetPanelName)
    {
        if (currentPanelName == targetPanelName)
            return;

        // Get references to current and target panels
        var currentPanel = panelDict[currentPanelName];
        var targetPanel = panelDict[targetPanelName];

        // Deactivate current panel
        currentPanel.panelObject.SetActive(false);

        // Activate target panel
        targetPanel.panelObject.SetActive(true);

        // Update current panel reference
        currentPanelName = targetPanelName;
    }

    public void GoBackToHome()
    {
        SwitchToPanel("Home");
    }
}