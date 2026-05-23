using UnityEngine;
using TMPro;

public class MapButton : MonoBehaviour
{
    [Header("Map Settings")]
    [Tooltip("The 360 Skybox Material for this map")]
    public Material previewSkybox;
    
    [Tooltip("The exact name of the Unity Scene to load")]
    public string sceneToLoad;
    
    [Tooltip("The text component on this button so we can change it to 'Start'")]
    public TMP_Text buttonTextComponent;

    private MainMenuController menuController;

    private void Start()
    {
        // Find the MainMenuController in the scene automatically
        menuController = FindObjectOfType<MainMenuController>();
    }

    // You can call this from your Button's OnClick() or XR Interactable SelectEntered event!
    public void ClickMapButton()
    {
        Debug.Log($"[MapButton] ClickMapButton was successfully triggered on {gameObject.name}!");

        if (menuController != null)
        {
            Debug.Log($"[MapButton] Sending data to MainMenuController...");
            menuController.SelectMap(previewSkybox, sceneToLoad, buttonTextComponent);
        }
        else
        {
            Debug.LogError("[MapButton] No MainMenuController found in the scene! Did you create a MenuManager object and attach the script?");
        }
    }
}
