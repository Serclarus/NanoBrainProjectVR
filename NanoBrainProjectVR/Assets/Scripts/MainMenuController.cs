using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Needed for changing button text

public class MainMenuController : MonoBehaviour
{
    [Header("Menu Setup")]
    [Tooltip("The blank skybox (or generic room) when nothing is selected")]
    public Material defaultSkybox;

    // We keep track of what the user is currently previewing
    private string selectedSceneName = "";
    private TMP_Text selectedButtonText;
    private Color originalTextColor = Color.white;
    private string originalTextString = "";
    
    // Track the currently active 3D map so we can turn it off
    private GameObject currentlyActivePreview;
    
    // Optional: An object that gets toggled when a preview is active
    private GameObject currentlyLinkedToggleObject;

    private void Start()
    {
        // Start the menu with the blank default skybox
        if (defaultSkybox != null)
        {
            RenderSettings.skybox = defaultSkybox;
            DynamicGI.UpdateEnvironment(); // Force lighting to update to the new skybox
        }
    }

    /// <summary>
    /// Call this from your Map Button's Unity Event (e.g. OnClick or SelectEntered).
    /// </summary>
    /// <param name="mapPreviewObject">The deactivated 3D geometry of the map.</param>
    /// <param name="sceneToLoad">The exact name of the Unity Scene to load.</param>
    /// <param name="buttonTextComponent">The TMP_Text component on the button so we can change it to 'Start'.</param>
    public void SelectMap(GameObject mapPreviewObject, string sceneToLoad, TMP_Text buttonTextComponent, GameObject linkedToggleObject = null)
    {
        // 1. If they click the SAME button twice (which now says "Start"), we load the game!
        if (selectedSceneName == sceneToLoad)
        {
            Debug.Log($"Loading Scene: {sceneToLoad}");
            if (buttonTextComponent != null) 
            {
                buttonTextComponent.text = "Loading...";
                buttonTextComponent.color = Color.yellow;
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            return;
        }

        // 2. If they click a NEW map button, we reset the old button (if there was one)
        if (selectedButtonText != null)
        {
            selectedButtonText.text = originalTextString; // Revert to its original exact text (e.g., "Shooting Range")
            selectedButtonText.color = originalTextColor; // Revert to original color
        }

        // 3. Turn OFF the old 3D map preview and revert its linked object
        if (currentlyActivePreview != null)
        {
            currentlyActivePreview.SetActive(false);
        }
        if (currentlyLinkedToggleObject != null)
        {
            currentlyLinkedToggleObject.SetActive(!currentlyLinkedToggleObject.activeSelf); // Reverse it back!
        }

        // 4. Set the new preview state
        selectedSceneName = sceneToLoad;
        selectedButtonText = buttonTextComponent;
        currentlyActivePreview = mapPreviewObject;
        currentlyLinkedToggleObject = linkedToggleObject;
        
        if (selectedButtonText != null)
        {
            originalTextColor = selectedButtonText.color; // Save its normal color before turning it green
            originalTextString = selectedButtonText.text; // Save its normal text before turning it to Start
        }

        // 5. Turn ON the new 3D map preview and reverse its linked object!
        if (currentlyActivePreview != null)
        {
            currentlyActivePreview.SetActive(true);
        }
        if (currentlyLinkedToggleObject != null)
        {
            currentlyLinkedToggleObject.SetActive(!currentlyLinkedToggleObject.activeSelf); // Toggle it!
        }

        // 6. Change this button's text to "Start" in Green!
        if (selectedButtonText != null)
        {
            selectedButtonText.text = "Start";
            selectedButtonText.color = Color.green;
        }
    }

    /// <summary>
    /// Optional: A button to cancel the preview and go back to the blank room
    /// </summary>
    public void CancelPreview()
    {
        selectedSceneName = "";
        
        if (selectedButtonText != null)
        {
            selectedButtonText.text = originalTextString;
            selectedButtonText.color = originalTextColor;
            selectedButtonText = null;
        }

        if (currentlyActivePreview != null)
        {
            currentlyActivePreview.SetActive(false);
            currentlyActivePreview = null;
        }
        
        if (currentlyLinkedToggleObject != null)
        {
            currentlyLinkedToggleObject.SetActive(!currentlyLinkedToggleObject.activeSelf); // Reverse it back
            currentlyLinkedToggleObject = null;
        }
    }
}
