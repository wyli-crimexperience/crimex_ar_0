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
    [SerializeField] private float minTimeBetweenSounds = 0.05f; // Prevent audio spam

    [Header("Rotation Audio Settings")]
    [SerializeField] private float rotationSoundInterval = 15f;
    private float lastRotationAngle = 0f;
    private bool isRotating = false;

    // Debouncing
    private float lastButtonPressTime = 0f;

    public static UIAudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
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
            audioSource.spatialBlend = 0f; // 2D audio for UI
            audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = uiAudioMixerGroup;

            audioSourcePool[i] = audioSource;
        }
    }

    private AudioSource GetNextAudioSource()
    {
        // Simple round-robin approach - more reliable than queue system
        AudioSource source = audioSourcePool[currentPoolIndex];
        currentPoolIndex = (currentPoolIndex + 1) % audioSourcePoolSize;
        return source;
    }

    private void PlayUISound(UIAudioClip uiClip, bool useHaptic = false, bool allowSpam = false)
    {
        // Null checks
        if (uiClip == null || uiClip.clips == null || uiClip.clips.Length == 0)
        {
            Debug.LogWarning("UIAudioManager: Trying to play null or empty audio clip");
            return;
        }

        // Debouncing for button presses
        if (!allowSpam && Time.time - lastButtonPressTime < minTimeBetweenSounds)
        {
            return;
        }

        AudioSource source = GetNextAudioSource();

        // Stop any currently playing sound on this source
        if (source.isPlaying)
        {
            source.Stop();
        }

        // Get random clip for variation
        AudioClip clipToPlay = uiClip.clips[Random.Range(0, uiClip.clips.Length)];

        // Validate clip
        if (clipToPlay == null)
        {
            Debug.LogWarning("UIAudioManager: Selected audio clip is null");
            return;
        }

        // Apply settings
        source.clip = clipToPlay;
        source.volume = uiClip.volume * masterUIVolume;
        source.pitch = uiClip.pitch + Random.Range(-uiClip.pitchVariation, uiClip.pitchVariation);

        // Use PlayOneShot for UI sounds to avoid clip assignment issues
        source.PlayOneShot(clipToPlay, uiClip.volume * masterUIVolume);

        // Handle haptic feedback
        if (useHaptic && enableHapticFeedback)
        {
            TriggerHapticFeedback();
        }

        lastButtonPressTime = Time.time;
    }

    private void TriggerHapticFeedback()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    // Public methods for UI interactions
    public void PlayButtonPress()
    {
        PlayUISound(buttonPress, true);
    }

    public void PlayButtonHover()
    {
        PlayUISound(buttonHover, false, true); // Allow spam for hover
    }

    public void PlayObjectSelect()
    {
        PlayUISound(objectSelect, true);
    }

    public void PlayObjectDeselect()
    {
        PlayUISound(objectDeselect, false);
    }

    public void PlayMenuOpen()
    {
        PlayUISound(menuOpen, false);
    }

    public void PlayMenuClose()
    {
        PlayUISound(menuClose, false);
    }

    public void PlayError()
    {
        PlayUISound(error, true);
    }

    public void PlaySuccess()
    {
        PlayUISound(success, true);
    }

    // Rotation audio with interval-based triggering
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

    public void StopRotation()
    {
        isRotating = false;
    }

    public void PlayScaleSound(float scaleMultiplier)
    {
        if (objectScale == null || objectScale.clips == null || objectScale.clips.Length == 0)
            return;

        AudioSource source = GetNextAudioSource();

        if (source.isPlaying)
        {
            source.Stop();
        }

        AudioClip clipToPlay = objectScale.clips[Random.Range(0, objectScale.clips.Length)];

        if (clipToPlay == null) return;

        float pitchModifier = Mathf.Clamp(scaleMultiplier, 0.5f, 2f);
        float finalPitch = objectScale.pitch * pitchModifier;

        source.PlayOneShot(clipToPlay, objectScale.volume * masterUIVolume);
        source.pitch = finalPitch;
    }

    // Volume control
    public void SetMasterUIVolume(float volume)
    {
        masterUIVolume = Mathf.Clamp01(volume);
    }

    public void SetHapticFeedback(bool enabled)
    {
        enableHapticFeedback = enabled;
    }

    public void StopAllUISounds()
    {
        foreach (AudioSource source in audioSourcePool)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }

    // Debug method to test if audio is working
    public void TestButtonPress()
    {
        Debug.Log("Testing button press audio...");
        PlayButtonPress();
    }
}