using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MicroscopyNavigator : MonoBehaviour
{   
    // Information Pages
    public void GoToBCMInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("BCM_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToPLMInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("PLM_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    // AR Pages
    public void GoToBCMAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("BCM_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToPLMAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("PLM_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

}

