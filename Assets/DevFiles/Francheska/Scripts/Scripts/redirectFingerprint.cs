using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class FingerprintNavigator : MonoBehaviour
{
    public void GoToMagneticBrushInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationMagneticBrush");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToMagneticBrushAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("MagneticBrushAR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToBlackPowderInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationBlackPowder");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToBlackPowderAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("BlackPowderAR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }


}

