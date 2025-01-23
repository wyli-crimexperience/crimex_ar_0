using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MicroscopeNavigator : MonoBehaviour
{
    public void GoToBulletInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationBullet");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToBulletAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("BulletAR");
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

