using UnityEngine;
using UnityEngine.UI;

public class UIButton : MonoBehaviour
{
    private Button button;

    void Start()
    {
        button = GetComponent<Button>();

        // Add the audio callback directly in code
        button.onClick.AddListener(() => {
            UIAudioManager.Instance.PlayButtonPress();
        });
    }
}