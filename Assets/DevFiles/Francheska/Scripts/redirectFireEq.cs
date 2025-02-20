using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
public class SceneNavigator : MonoBehaviour
{
    public void GoToFireExtinguisherInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationFireExt");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToFireExtinguisherAR()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("FireExtinguisherARModel");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }

    public void GoToFiremanHatInfo()
    {
        if (NavigatorManager.Instance != null)
        {
            NavigatorManager.Instance.LoadScene("InformationFiremanHat");
        }
        else
        {
            Debug.LogError("NavigatorManager instance is missing!");
        }
    }
}