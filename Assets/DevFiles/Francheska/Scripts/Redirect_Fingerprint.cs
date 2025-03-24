using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class FingerprintNavigator : MonoBehaviour
{   
    // Information Pages
    public void GoToFeatherBrushInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("FeatherBrush_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToFibreBrushInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("FibreBrush_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToFingerprintBrushesInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("FingerprintBrush_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToMagneticBrushInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("MagneticBrush_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToPowderInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("Powder_info");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
  
    // AR Pages

    public void GoToPowdersAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("powders_AR");
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
            NavigatorManager.Instance.LoadScene("magneticbrush_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToFeatherBrushAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("featherbrush_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToFiberBrushAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("fiberbrush_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
    public void GoToFingerprintBrushesAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("fingerprintbrushes_AR");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

}

