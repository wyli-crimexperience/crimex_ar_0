using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class KitNavigator : MonoBehaviour
{   
    // Information Pages
    public void GoToKitInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("EvidenceKit_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToMarkerInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("EvidenceMarkers_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    // AR Pages
    public void GoToMarkerAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("evidencemarkers_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToCollectionAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("collection_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToMeasurementAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("measurement_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToPackagingAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("packaging_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToVisualizationAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("visualization_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
}

