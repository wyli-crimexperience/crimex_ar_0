using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class PhotographyNavigator : MonoBehaviour
{   
    // Information Pages
    public void GoToDSLRInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("DSLR_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToALSInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("ALS_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }


    // AR Pages
    public void GoToDSLRAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("DSLR_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToALSAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("ALS_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

}

