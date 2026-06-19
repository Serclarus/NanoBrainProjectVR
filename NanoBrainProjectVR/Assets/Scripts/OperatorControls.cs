using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System.Collections.Generic;

[System.Serializable]
public class SceneShortcut
{
    public Key shortcutKey;
    public string sceneToLoad;
}

public class OperatorControls : MonoBehaviour
{
    [Header("Operator PC Controls")]
    [Tooltip("Key to instantly load the Main Menu")]
    public Key mainMenuKey = Key.F1;
    public string mainMenuSceneName = "MainMenu";

    [Tooltip("Key to instantly reset the current scene")]
    public Key resetSceneKey = Key.F2;

    [Tooltip("Key to pause/unpause the game")]
    public Key pauseKey = Key.Escape;

    [Tooltip("If true, this script will stay alive even when you change scenes, so you only need to add it to your starting scene.")]
    public bool keepAliveAcrossScenes = true;

    [Header("Scene Shortcuts")]
    [Tooltip("Pressing these keys will instantly load the corresponding scene from anywhere")]
    public List<SceneShortcut> sceneShortcuts = new List<SceneShortcut>();

    private static OperatorControls instance;
    private bool isPaused = false;

    private void Awake()
    {
        if (keepAliveAcrossScenes)
        {
            // Ensure only one instance exists if we reload the scene this originated from
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[mainMenuKey].wasPressedThisFrame)
        {
            LoadMainMenu();
        }

        if (keyboard[resetSceneKey].wasPressedThisFrame)
        {
            ResetCurrentScene();
        }

        if (keyboard[pauseKey].wasPressedThisFrame)
        {
            TogglePause();
        }

        foreach (var shortcut in sceneShortcuts)
        {
            if (shortcut.shortcutKey != Key.None && keyboard[shortcut.shortcutKey].wasPressedThisFrame)
            {
                Debug.Log($"[OperatorControls] Operator requested load for Scene: {shortcut.sceneToLoad}");
                if (isPaused) TogglePause();

                if (VRSceneFader.Instance != null) {
                    VRSceneFader.Instance.FadeToScene(shortcut.sceneToLoad);
                } else {
                    SceneManager.LoadScene(shortcut.sceneToLoad);
                }
            }
        }
#else
        // Fallback for Legacy Input System just in case
        if (Input.GetKeyDown(KeyCode.F1))
        {
            LoadMainMenu();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            ResetCurrentScene();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
#endif
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        Debug.Log($"[OperatorControls] Game is now {(isPaused ? "PAUSED" : "UNPAUSED")}");
    }

    public void LoadMainMenu()
    {
        Debug.Log("[OperatorControls] Operator requested Main Menu load...");
        
        // Ensure game isn't frozen, otherwise the fade animation won't play!
        if (isPaused) TogglePause();

        if (VRSceneFader.Instance != null) {
            VRSceneFader.Instance.FadeToScene(mainMenuSceneName);
        } else {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public void ResetCurrentScene()
    {
        Debug.Log("[OperatorControls] Operator requested Scene Reset...");
        
        if (isPaused) TogglePause();

        string currentScene = SceneManager.GetActiveScene().name;
        if (VRSceneFader.Instance != null) {
            VRSceneFader.Instance.FadeToScene(currentScene);
        } else {
            SceneManager.LoadScene(currentScene);
        }
    }
}
