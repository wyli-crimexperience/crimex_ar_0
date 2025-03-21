using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class SurveillanceNavigator : MonoBehaviour
{   
    // Information Pages
    public void GoToPoliceLineInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("PoliceLine_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToPolygraphInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("Polygraph_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    // AR Pages
    public void GoToPoliceLineAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("policeline_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToPolygraphAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("polygraph_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

}

