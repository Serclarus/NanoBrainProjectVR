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

    private MainMenuController menuController;

    private float lastPressTime = 0f;
    private float cooldownDelay = 1.0f; // 1 second cooldown between touches

    private void Start()
    {
        // Find the MainMenuController in the scene automatically
        menuController = FindObjectOfType<MainMenuController>();
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
