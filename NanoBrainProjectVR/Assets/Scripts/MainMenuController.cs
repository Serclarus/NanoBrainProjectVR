using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // Needed for changing button text
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public class MainMenuController : MonoBehaviour
{
    [Header("Menu Setup")]
    [Tooltip("The blank skybox (or generic room) when nothing is selected")]
    public Material defaultSkybox;

    [Header("Layer Culling Setup")]
    [Tooltip("If true, swaps layers to hide maps instead of deactivating them (prevents lag spikes)")]
    public bool useLayerCulling = true;
    public string visibleLayerName = "Default";
    public string hiddenLayerName = "HideFromCamera";

    // We keep track of what the user is currently previewing
    private string selectedSceneName = "";
    private TMP_Text selectedButtonText;
    private Color originalTextColor = Color.white;
    private string originalTextString = "";
    
    // Safety timer to prevent accidental double-clicks from loading the scene instantly
    private float timeWhenPreviewed = -10f;
    private float previewLockDuration = 1.5f; // Must wait 1.5 seconds after previewing before you can click Start
    
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

    private void Update()
    {
        // Do NOT cache the result! If a dummy player exists in the scene, the cache will latch onto the dummy
        // and completely ignore the real player when they spawn! We must search the entire scene every frame!
        
        var providers = FindObjectsOfType<LocomotionProvider>();
        foreach (var provider in providers)
        {
            if (provider != null && provider.enabled) provider.enabled = false;
        }
        
        var controllers = FindObjectsOfType<CharacterController>();
        foreach (var controller in controllers)
        {
            if (controller != null && controller.enabled) controller.enabled = false;
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
            // SAFETY: Ignore the click if they just previewed it a split second ago (prevents VR hand multi-collider double clicks)
            if (Time.unscaledTime < timeWhenPreviewed + previewLockDuration)
            {
                Debug.Log($"[MainMenuController] Ignored accidental double-click on {sceneToLoad} (Wait {previewLockDuration}s)");
                return;
            }

            Debug.Log($"Loading Scene: {sceneToLoad}");
            if (buttonTextComponent != null) 
            {
                buttonTextComponent.text = "Loading...";
                buttonTextComponent.color = Color.yellow;
            }

            // Use the smooth fade if it exists!
            if (VRSceneFader.Instance != null) {
                VRSceneFader.Instance.FadeToScene(sceneToLoad);
            } else {
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            }
            return;
        }

        // 2. If they click a NEW map button, we reset the old button
        if (selectedButtonText != null)
        {
            selectedButtonText.text = originalTextString; // Revert text
            selectedButtonText.color = originalTextColor; // Revert color
        }

        timeWhenPreviewed = Time.unscaledTime; // Start the safety lock timer

        // If VRSceneFader exists, do a quick Blink transition!
        if (VRSceneFader.Instance != null)
        {
            VRSceneFader.Instance.Blink(() =>
            {
                SwapMapGeometry(mapPreviewObject, sceneToLoad, buttonTextComponent, linkedToggleObject);
            });
        }
        else
        {
            SwapMapGeometry(mapPreviewObject, sceneToLoad, buttonTextComponent, linkedToggleObject);
        }
    }

    private void SwapMapGeometry(GameObject mapPreviewObject, string sceneToLoad, TMP_Text buttonTextComponent, GameObject linkedToggleObject)
    {
        // 3. Turn OFF the old 3D map preview and revert its linked object
        if (currentlyActivePreview != null)
        {
            if (useLayerCulling) SetLayerRecursively(currentlyActivePreview, LayerMask.NameToLayer(hiddenLayerName));
            else currentlyActivePreview.SetActive(false);
        }
        if (currentlyLinkedToggleObject != null) currentlyLinkedToggleObject.SetActive(!currentlyLinkedToggleObject.activeSelf);

        // 4. Set the new preview state
        selectedSceneName = sceneToLoad;
        selectedButtonText = buttonTextComponent;
        currentlyActivePreview = mapPreviewObject;
        currentlyLinkedToggleObject = linkedToggleObject;
        
        if (selectedButtonText != null)
        {
            originalTextColor = selectedButtonText.color;
            originalTextString = selectedButtonText.text;
        }

        // 5. Turn ON the new 3D map preview and reverse its linked object!
        if (currentlyActivePreview != null)
        {
            if (useLayerCulling) SetLayerRecursively(currentlyActivePreview, LayerMask.NameToLayer(visibleLayerName));
            else currentlyActivePreview.SetActive(true);
        }
        if (currentlyLinkedToggleObject != null) currentlyLinkedToggleObject.SetActive(!currentlyLinkedToggleObject.activeSelf);

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
        if (VRSceneFader.Instance != null)
        {
            VRSceneFader.Instance.Blink(() => { ExecuteCancelPreview(); });
        }
        else
        {
            ExecuteCancelPreview();
        }
    }

    private void ExecuteCancelPreview()
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
            if (useLayerCulling) SetLayerRecursively(currentlyActivePreview, LayerMask.NameToLayer(hiddenLayerName));
            else currentlyActivePreview.SetActive(false);
            
            currentlyActivePreview = null;
        }
        
        if (currentlyLinkedToggleObject != null)
        {
            currentlyLinkedToggleObject.SetActive(!currentlyLinkedToggleObject.activeSelf);
            currentlyLinkedToggleObject = null;
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null || newLayer == -1) return;
        
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
