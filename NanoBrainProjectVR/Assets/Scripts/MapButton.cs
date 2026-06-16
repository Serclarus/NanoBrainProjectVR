using UnityEngine;
using TMPro;

public class MapButton : MonoBehaviour
{
    [Header("Map Settings")]
    [Tooltip("The 3D Object containing your map geometry (keep it unchecked/deactivated)")]
    public GameObject mapPreviewObject;
    
    [Tooltip("The exact name of the Unity Scene to load")]
    public string sceneToLoad;
    
    [Tooltip("The text component on this button so we can change it to 'Start'")]
    public TMP_Text buttonTextComponent;

    [Tooltip("Optional: An object that will reverse its active state when this map is previewed (e.g., hiding a default room)")]
    public GameObject linkedToggleObject;

    [Header("Debug Controls")]
    [Tooltip("Optional: Press this keyboard key to trigger this map button from the PC (e.g., NumPad1)")]
    public UnityEngine.InputSystem.Key debugKeyboardKey = UnityEngine.InputSystem.Key.None;

    private MainMenuController menuController;

    private float lastPressTime = -10f;
    private float cooldownDelay = 0.5f; // 0.5 second cooldown between any touches

    private void Start()
    {
        // Find the MainMenuController in the scene automatically
        menuController = FindObjectOfType<MainMenuController>();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && debugKeyboardKey != UnityEngine.InputSystem.Key.None)
        {
            if (keyboard[debugKeyboardKey].wasPressedThisFrame)
            {
                Debug.Log($"[MapButton] Keyboard override triggered for {gameObject.name}");
                ClickMapButton();
            }
        }
#endif
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react if the object touching the button is a hand/controller
        if (other.CompareTag("Player") || other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller"))
        {
            ClickMapButton();
        }
    }

    public void ClickMapButton()
    {
        // Prevent double-clicks across all event types (UnityEvents + Triggers)
        if (Time.unscaledTime < lastPressTime + cooldownDelay) return;
        lastPressTime = Time.unscaledTime;

        Debug.Log($"[MapButton] ClickMapButton was successfully triggered on {gameObject.name}!");

        if (menuController != null)
        {
            Debug.Log($"[MapButton] Sending data to MainMenuController...");
            menuController.SelectMap(mapPreviewObject, sceneToLoad, buttonTextComponent, linkedToggleObject);
        }
        else
        {
            Debug.LogError("[MapButton] No MainMenuController found in the scene! Did you create a MenuManager object and attach the script?");
        }
    }
}
