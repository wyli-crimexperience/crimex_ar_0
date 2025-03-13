using UnityEngine;
using UnityEngine.Events;

public class TouchHandler : MonoBehaviour
{
    public static TouchHandler Instance { get; private set; }
    public UnityEvent<Touch> onTouch = new UnityEvent<Touch>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        foreach (var touch in Input.touches)
        {
            Debug.Log("Touch detected at: " + touch.position); // Add this
            onTouch.Invoke(touch);
        }
    }
}
