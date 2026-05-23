using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Needed for changing button text

public class MainMenuController : MonoBehaviour
{
    [Header("Skybox Settings")]
    [Tooltip("The blank, neutral skybox for the main menu")]
    public Material defaultSkybox;
    
    // We keep track of what the user is currently previewing
    private string selectedSceneName = "";
    private TMP_Text selectedButtonText;
    private Color originalTextColor = Color.white;
    private string originalTextString = "";

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
    /// <param name="previewSkybox">The 360 skybox material for this specific map.</param>
    /// <param name="sceneToLoad">The exact name of the Unity Scene to load.</param>
    /// <param name="buttonTextComponent">The TMP_Text component on the button so we can change it to 'Start'.</param>
    public void SelectMap(Material previewSkybox, string sceneToLoad, TMP_Text buttonTextComponent)
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
            SceneManager.LoadScene(sceneToLoad);
            return;
        }

        // 2. If they click a NEW map button, we reset the old button (if there was one)
        if (selectedButtonText != null)
        {
            selectedButtonText.text = originalTextString; // Revert to its original exact text (e.g., "Shooting Range")
            selectedButtonText.color = originalTextColor; // Revert to original color
        }

        // 3. Set the new preview state
        selectedSceneName = sceneToLoad;
        selectedButtonText = buttonTextComponent;
        
        if (selectedButtonText != null)
        {
            originalTextColor = selectedButtonText.color; // Save its normal color before turning it green
            originalTextString = selectedButtonText.text; // Save its normal text before turning it to Start
        }

        // 4. Change the skybox to magically immerse them in the preview!
        if (previewSkybox != null)
        {
            RenderSettings.skybox = previewSkybox;
            DynamicGI.UpdateEnvironment(); // Update the lighting to match the new skybox!
        }

        // 5. Change this button's text to "Start" in Green!
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

        if (defaultSkybox != null)
        {
            RenderSettings.skybox = defaultSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }
}
