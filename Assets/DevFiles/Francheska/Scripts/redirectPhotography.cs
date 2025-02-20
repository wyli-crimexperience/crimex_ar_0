using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class PhotographyNavigator : MonoBehaviour
{
    public void GoToDSLRInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationDSLR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToDSLRAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("dslrAR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }




}

