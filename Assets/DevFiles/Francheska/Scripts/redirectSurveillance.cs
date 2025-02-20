using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class SurveillanceNavigator : MonoBehaviour
{
    public void GoToPolygraphInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationPolygraph");
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
            NavigatorManager.Instance.LoadScene("PolygraphAR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToTapeInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationPoliceTape");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }



}

