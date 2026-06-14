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

    private float lastPressTime = 0f;
    private float cooldownDelay = 1.0f; // 1 second cooldown between touches

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

    // This automatically detects physical touches from your ghost hands!
    // No need to mess with Interaction Layers or XR Simple Interactable settings.
    private void OnTriggerEnter(Collider other)
    {
        // Prevent double-touching instantly
        if (Time.time < lastPressTime + cooldownDelay) return;

        // Only react if the object touching the button is a hand/controller
        if (other.CompareTag("Player") || other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller"))
        {
            lastPressTime = Time.time;
            ClickMapButton();
        }
    }

    // You can also call this from your Button's OnClick() if you want!
    public void ClickMapButton()
    {
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
