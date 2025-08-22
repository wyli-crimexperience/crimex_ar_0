using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class UIAudioManager : MonoBehaviour
{
    [System.Serializable]
    public class UIAudioClip
    {
        public string name;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        [Range(0f, 1f)] public float pitchVariation = 0.1f;
    }

    [Header("Audio Sources")]
    [SerializeField] private int audioSourcePoolSize = 5;
    private AudioSource[] audioSourcePool;
    private int currentPoolIndex = 0;

    [Header("UI Audio Clips")]
    [SerializeField] private UIAudioClip buttonPress;
    [SerializeField] private UIAudioClip buttonHover;
    [SerializeField] private UIAudioClip objectSelect;
    [SerializeField] private UIAudioClip objectDeselect;
    [SerializeField] private UIAudioClip objectRotate;
    [SerializeField] private UIAudioClip objectScale;
    [SerializeField] private UIAudioClip menuOpen;
    [SerializeField] private UIAudioClip menuClose;
    [SerializeField] private UIAudioClip error;
    [SerializeField] private UIAudioClip success;

    [Header("Audio Settings")]
    [SerializeField] private AudioMixerGroup uiAudioMixerGroup;
    [SerializeField][Range(0f, 1f)] private float masterUIVolume = 1f;
    [SerializeField] private bool enableHapticFeedback = true;
    [SerializeField] private float minTimeBetweenSounds = 0.05f;

    [Header("Rotation Audio Settings")]
    [SerializeField] private float rotationSoundInterval = 15f;
    private float lastRotationAngle = 0f;
    private bool isRotating = false;

    // Debouncing
    private float lastButtonPressTime = 0f;

    public static UIAudioManager Instance { get; private set; }

    // Android vibrator
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator;
#endif

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            InitHaptics();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioSources()
    {
        audioSourcePool = new AudioSource[audioSourcePoolSize];

        for (int i = 0; i < audioSourcePoolSize; i++)
        {
            GameObject audioSourceObject = new GameObject($"UIAudioSource_{i}");
            audioSourceObject.transform.SetParent(transform);

            AudioSource audioSource = audioSourceObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = uiAudioMixerGroup;

            audioSourcePool[i] = audioSource;
        }
    }

    private void InitHaptics()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
#endif
    }

    private AudioSource GetNextAudioSource()
    {
        AudioSource source = audioSourcePool[currentPoolIndex];
        currentPoolIndex = (currentPoolIndex + 1) % audioSourcePoolSize;
        return source;
    }

    private void PlayUISound(UIAudioClip uiClip, bool useHaptic = false, bool allowSpam = false)
    {
        if (uiClip == null || uiClip.clips == null || uiClip.clips.Length == 0) return;

        if (!allowSpam && Time.time - lastButtonPressTime < minTimeBetweenSounds) return;

        AudioSource source = GetNextAudioSource();

        if (source.isPlaying) source.Stop();

        AudioClip clipToPlay = uiClip.clips[Random.Range(0, uiClip.clips.Length)];
        if (clipToPlay == null) return;

        source.clip = clipToPlay;
        source.volume = uiClip.volume * masterUIVolume;
        source.pitch = uiClip.pitch + Random.Range(-uiClip.pitchVariation, uiClip.pitchVariation);

        source.PlayOneShot(clipToPlay, uiClip.volume * masterUIVolume);

        if (useHaptic && enableHapticFeedback) TriggerHapticFeedback();

        lastButtonPressTime = Time.time;
    }

    private void TriggerHapticFeedback()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator != null)
        {
            // Simple short pulse (~30ms)
            vibrator.Call("vibrate", 30L);
        }
#elif UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    public void TriggerHapticPattern()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator != null)
        {
            // Pattern: delay 0ms, vibrate 40ms, pause 30ms, vibrate 40ms
            long[] pattern = { 0, 40, 30, 40 };
            vibrator.Call("vibrate", pattern, -1); // -1 means no repeat
        }
#endif
    }

    // === Public Methods ===
    public void PlayButtonPress() => PlayUISound(buttonPress, true);
    public void PlayButtonHover() => PlayUISound(buttonHover, false, true);
    public void PlayObjectSelect() => PlayUISound(objectSelect, true);
    public void PlayObjectDeselect() => PlayUISound(objectDeselect, false);
    public void PlayMenuOpen() => PlayUISound(menuOpen, false);
    public void PlayMenuClose() => PlayUISound(menuClose, false);
    public void PlayError() => PlayUISound(error, true);
    public void PlaySuccess() => PlayUISound(success, true);

    public void StartRotation(float currentAngle)
    {
        lastRotationAngle = currentAngle;
        isRotating = true;
    }

    public void UpdateRotation(float currentAngle)
    {
        if (!isRotating) return;
        float angleDifference = Mathf.Abs(currentAngle - lastRotationAngle);
        if (angleDifference >= rotationSoundInterval)
        {
            PlayUISound(objectRotate, false, true);
            lastRotationAngle = currentAngle;
        }
    }

    public void StopRotation() => isRotating = false;

    public void PlayScaleSound(float scaleMultiplier)
    {
        if (objectScale == null || objectScale.clips == null || objectScale.clips.Length == 0) return;

        AudioSource source = GetNextAudioSource();
        if (source.isPlaying) source.Stop();

        AudioClip clipToPlay = objectScale.clips[Random.Range(0, objectScale.clips.Length)];
        if (clipToPlay == null) return;

        float pitchModifier = Mathf.Clamp(scaleMultiplier, 0.5f, 2f);
        float finalPitch = objectScale.pitch * pitchModifier;

        source.PlayOneShot(clipToPlay, objectScale.volume * masterUIVolume);
        source.pitch = finalPitch;
    }

    public void SetMasterUIVolume(float volume) => masterUIVolume = Mathf.Clamp01(volume);
    public void SetHapticFeedback(bool enabled) => enableHapticFeedback = enabled;

    public void StopAllUISounds()
    {
        foreach (AudioSource source in audioSourcePool)
            if (source.isPlaying) source.Stop();
    }

    public void TestButtonPress()
    {
        Debug.Log("Testing button press audio...");
        PlayButtonPress();
    }
}
