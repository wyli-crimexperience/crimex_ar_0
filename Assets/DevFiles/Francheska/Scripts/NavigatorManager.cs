using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class NavigatorManager : MonoBehaviour
{
    // Singleton instance
    public static NavigatorManager Instance { get; private set; }

    // List to track the scene history
    private static List<string> sceneHistory = new List<string>();

    private void Awake()
    {
        // Ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(transform.root.gameObject);
        }
    }

    // Function to load a specific scene
    public void LoadScene(string sceneName)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        // Save the current scene before loading the next one
        if (currentScene != sceneName)
        {
            sceneHistory.Add(currentScene);
            Debug.Log($"Scene added to history: {currentScene}");
        }

        Debug.Log("Current scene history: " + string.Join(", ", sceneHistory));

        // Load the specified scene
        SceneManager.LoadScene(sceneName);
    }

    // Function to go back to the previous scene
    public void GoBackToPreviousScene()
    {
        if (sceneHistory.Count > 0)
        {
            string previousScene = sceneHistory[sceneHistory.Count - 1];
            sceneHistory.RemoveAt(sceneHistory.Count - 1); // Remove the last scene from the history
            Debug.Log($"Going back to previous scene: {previousScene}");
            Debug.Log("Updated scene history: " + string.Join(", ", sceneHistory));

            SceneManager.LoadScene(previousScene);
        }
        else
        {
            Debug.LogWarning("No previous scene to go back to.");
        }
    }

    // Example category-specific methods
    public void GoToEvidenceCollectionModels()
    {
        LoadScene("EvidenceKitModels");
    }

    public void GoToSurveillanceModels()
    {
        LoadScene("SurveillanceModels");
    }

    public void GoToFireEquipmentModels()
    {
        LoadScene("FireEquipmentModels");
    }
    public void GoToFingerprintModels()
    {
        LoadScene("FingerprintModels");
    }
    public void GoToPhotographyModels()
    {
        LoadScene("PhotographyModels");
    }
    public void GoToMicroscopyModels()
    {
        LoadScene("MicroscopyModels");
    }
    
    public void GoToChangeScene()
    {
        LoadScene("");
    }
}
