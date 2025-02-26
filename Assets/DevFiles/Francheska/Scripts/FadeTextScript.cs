using UnityEngine;
using TMPro;

public class FadeTextScript : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private float fadeTime = 3.0f;
    [SerializeField] private float fadeAwayPerSecond;
    [SerializeField] private float alphaValue;

    private TextMeshProUGUI fadeAwayText;
    private bool isFading = false;

    void Start()
    {
        fadeAwayText = GetComponent<TextMeshProUGUI>();
        if (fadeAwayText == null)
        {
            Debug.LogError("TextMeshProUGUI component not found!");
            enabled = false;
            return;
        }

        fadeAwayPerSecond = 1f / fadeTime;
        alphaValue = fadeAwayText.color.a;
    }

    void Update()
    {
        if (isFading && fadeTime > 0)
        {
            alphaValue -= fadeAwayPerSecond * Time.deltaTime;
            fadeAwayText.color = new Color(
                fadeAwayText.color.r,
                fadeAwayText.color.g,
                fadeAwayText.color.b,
                Mathf.Clamp01(alphaValue)
            );

            fadeTime -= Time.deltaTime;

            if (fadeTime <= 0)
            {
                isFading = false;
                fadeAwayText.color = new Color(
                    fadeAwayText.color.r,
                    fadeAwayText.color.g,
                    fadeAwayText.color.b,
                    0f
                );
            }
        }
    }

    public void StartFade()
    {
        if (!isFading)
        {
            isFading = true;
            fadeTime = 3.0f;
            alphaValue = fadeAwayText.color.a;
        }
    }

    public void ResetFade()
    {
        isFading = false;
        fadeTime = 3.0f;
        alphaValue = 1f;
        fadeAwayText.color = new Color(
            fadeAwayText.color.r,
            fadeAwayText.color.g,
            fadeAwayText.color.b,
            alphaValue
        );
    }
}