using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class KitNavigator : MonoBehaviour
{
    public void GoToKitInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationKit");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToRulerInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationRuler");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToRulerAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("LRulerAR");
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
            NavigatorManager.Instance.LoadScene("InformationMarkers");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToMarkerAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("MarkersAR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
}

