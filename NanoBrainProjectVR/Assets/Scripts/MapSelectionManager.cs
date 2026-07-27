using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class MapButtonData
{
    [Header("Setup")]
    [Tooltip("The physical 3D button GameObject that the player touches")]
    public GameObject buttonObject;
    
    [Tooltip("The TextMeshPro text component on this button")]
    public TMP_Text buttonText;
    
    [Tooltip("The 3D map environment to turn on when previewing")]
    public GameObject previewObject;
    
    [Tooltip("The exact name of the Scene to load")]
    public string sceneToLoad;

    [Tooltip("Optional: Press this keyboard key to trigger this map button from the PC (e.g., NumPad1)")]
    public UnityEngine.InputSystem.Key debugKeyboardKey = UnityEngine.InputSystem.Key.None;

    // Internal state tracking
    [HideInInspector] public string originalTextString;
    [HideInInspector] public Color originalTextColor;
}

public class MapSelectionManager : MonoBehaviour
{
    [Header("Menu Environment")]
    [Tooltip("The default blank skybox when no map is previewed")]
    public Material defaultSkybox;

    [Header("UI Settings")]
    [Tooltip("Text element to display server connection status")]
    public TMP_Text connectionStatusText;

    [Header("Testing")]
    [Tooltip("If true, playing in the Unity Editor will act like a VR headset and join as a Client instead of hosting as a PC Operator.")]
    public bool forceVRInEditor = true;

    [Header("Layer Culling Setup")]
    [Tooltip("If true, swaps layers to hide maps instead of deactivating them (prevents lag spikes)")]
    public bool useLayerCulling = true;
    public string visibleLayerName = "Default";
    public string hiddenLayerName = "HideFromCamera";

    [Header("Map Buttons")]
    public List<MapButtonData> maps = new List<MapButtonData>();

    // Safety and State
    private int currentlyPreviewedIndex = -1;
    private float timeWhenPreviewed = -10f;
    private float previewLockDuration = 0.75f; // Wait 0.75s after preview before "Start" works

    private void Start()
    {
        // 1. Set the default skybox
        if (defaultSkybox != null)
        {
            RenderSettings.skybox = defaultSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // 2. Setup all buttons automatically!
        for (int i = 0; i < maps.Count; i++)
        {
            if (maps[i].buttonObject != null)
            {
                // Save their default appearance
                if (maps[i].buttonText != null)
                {
                    maps[i].originalTextString = maps[i].buttonText.text;
                    maps[i].originalTextColor = maps[i].buttonText.color;
                }

                // Make sure the preview object is off by default
                if (maps[i].previewObject != null)
                {
                    if (useLayerCulling) SetLayerRecursively(maps[i].previewObject, LayerMask.NameToLayer(hiddenLayerName));
                    else maps[i].previewObject.SetActive(false);
                }

                // Add our secret collision reporter to the button automatically
                MapButtonTrigger listener = maps[i].buttonObject.AddComponent<MapButtonTrigger>();
                listener.manager = this;
                listener.buttonIndex = i;
            }
        }
        
        // Subscribe to connection failures to enable endless retry loop!
        if (XRMultiplayer.XRINetworkGameManager.Instance != null)
        {
            XRMultiplayer.XRINetworkGameManager.Instance.OnConnectionFailedAction += HandleConnectionFailure;
        }

        // 3. Automatically host a local server so the Operator Dashboard works perfectly out-of-the-box
        Invoke(nameof(AutoHostLocal), 1f); // Wait 1 second to ensure NetworkManager is fully awake
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (XRMultiplayer.XRINetworkGameManager.Instance != null)
        {
            XRMultiplayer.XRINetworkGameManager.Instance.OnConnectionFailedAction -= HandleConnectionFailure;
        }

        // Re-enable locomotion when leaving the main menu!
        var providers = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var provider in providers)
        {
            if (provider != null && !provider.enabled) provider.enabled = true;
        }
        
        var controllers = FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            if (controller != null && !controller.enabled) controller.enabled = true;
        }
    }

    private void HandleConnectionFailure(string reason)
    {
        Debug.LogWarning($"[MapSelectionManager] Connection failed ({reason}). Retrying in 1 second...");
        Invoke(nameof(AutoHostLocal), 1f);
    }
    
    private void AutoHostLocal()
    {
        if (Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            if (XRMultiplayer.XRINetworkGameManager.Instance != null)
            {
                // Determine if we are running in VR or on PC
                bool isVRActive = (Application.platform == RuntimePlatform.Android) || (Application.isEditor && forceVRInEditor);
                
                if (!isVRActive)
                {
                    var xrDisplays = new List<UnityEngine.XR.XRDisplaySubsystem>();
                    UnityEngine.SubsystemManager.GetSubsystems(xrDisplays);
                    foreach (var display in xrDisplays)
                    {
                        if (display.running) isVRActive = true;
                    }
                }
                
                if (isVRActive)
                {
                    Debug.Log("[MapSelectionManager] VR Player Detected: Auto-joining Lobby as Client...");
                    XRMultiplayer.XRINetworkGameManager.Instance.QuickJoinLobby();
                }
                else
                {
                    Debug.Log("[MapSelectionManager] PC Operator Detected: Creating Lobby as Host...");
                    XRMultiplayer.XRINetworkGameManager.Instance.CreateNewLobby("Operator_Session", false, 10);
                }
            }
        }
    }

    private void Update()
    {
        // Update connection status text
        if (connectionStatusText != null && XRMultiplayer.XRINetworkGameManager.Instance != null)
        {
            var state = XRMultiplayer.XRINetworkGameManager.CurrentConnectionState.Value;
            if (state == XRMultiplayer.XRINetworkGameManager.ConnectionState.Connected)
            {
                connectionStatusText.text = "<color=green>Connected to Server</color>";
            }
            else if (state == XRMultiplayer.XRINetworkGameManager.ConnectionState.Connecting)
            {
                connectionStatusText.text = "<color=yellow>Joining Server...</color>";
            }
            else
            {
                connectionStatusText.text = "<color=red>Disconnected</color>";
            }
        }

        // Keyboard shortcuts have been moved to OperatorControls.cs

        // Disable all movement in the Main Menu so the player can't walk away from the UI!
        // We must search the entire scene every frame to catch dynamically spawned Network Players
        // or offline dummy objects.
        var providers = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>();
        foreach (var provider in providers)
        {
            if (provider != null && provider.enabled) provider.enabled = false;
        }
        
        // Aggressively paralyze the physical collision capsule so movement is absolutely impossible
        var controllers = FindObjectsOfType<CharacterController>();
        foreach (var controller in controllers)
        {
            if (controller != null && controller.enabled) controller.enabled = false;
        }
    }

    private float lastSwapTime = -10f;

    // Called automatically by the invisible MapButtonTrigger script
    public void HandleButtonPress(int index)
    {
        if (index < 0 || index >= maps.Count) return;

        MapButtonData clickedMap = maps[index];

        // 1. Did they click the button that is ALREADY previewing?
        if (currentlyPreviewedIndex == index)
        {
            // SAFETY: Ignore if they just touched it a split second ago
            if (Time.unscaledTime < timeWhenPreviewed + previewLockDuration)
            {
                return;
            }

            // LOAD THE SCENE!
            Debug.Log($"[MapSelectionManager] Loading Scene: {clickedMap.sceneToLoad}");
            if (clickedMap.buttonText != null)
            {
                clickedMap.buttonText.text = "Loading...";
                clickedMap.buttonText.color = Color.yellow;
            }

            if (VRSceneFader.Instance != null) {
                VRSceneFader.Instance.FadeToScene(clickedMap.sceneToLoad);
            } else {
                SceneManager.LoadScene(clickedMap.sceneToLoad);
            }
        }
        // 2. They clicked a NEW button!
        else
        {
            // Global cooldown to prevent rapid bouncing between different maps
            if (Time.unscaledTime < lastSwapTime + 0.5f) return;
            lastSwapTime = Time.unscaledTime;

            Debug.Log($"[MapSelectionManager] Previewing Map: {clickedMap.sceneToLoad}");

            // If VRSceneFader exists, blink to hide the transition
            if (VRSceneFader.Instance != null)
            {
                VRSceneFader.Instance.Blink(() => { ShowPreview(index); });
            }
            else
            {
                ShowPreview(index);
            }
        }
    }

    private void ShowPreview(int newIndex)
    {
        // A. Turn off the old preview and revert its button text
        if (currentlyPreviewedIndex != -1)
        {
            MapButtonData oldMap = maps[currentlyPreviewedIndex];
            if (oldMap.previewObject != null)
            {
                if (useLayerCulling) SetLayerRecursively(oldMap.previewObject, LayerMask.NameToLayer(hiddenLayerName));
                else oldMap.previewObject.SetActive(false);
            }
            if (oldMap.buttonText != null)
            {
                oldMap.buttonText.text = oldMap.originalTextString;
                oldMap.buttonText.color = oldMap.originalTextColor;
            }
        }

        // B. Update our state
        currentlyPreviewedIndex = newIndex;
        timeWhenPreviewed = Time.unscaledTime; // Start the safety timer!

        // C. Turn on the NEW preview and update its button text
        MapButtonData newMap = maps[currentlyPreviewedIndex];
        if (newMap.previewObject != null)
        {
            if (useLayerCulling) SetLayerRecursively(newMap.previewObject, LayerMask.NameToLayer(visibleLayerName));
            else newMap.previewObject.SetActive(true);
        }
        if (newMap.buttonText != null)
        {
            newMap.buttonText.text = "Start";
            newMap.buttonText.color = Color.green;
        }
    }

    public void CancelPreview()
    {
        if (currentlyPreviewedIndex == -1) return;

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
        if (currentlyPreviewedIndex != -1)
        {
            MapButtonData oldMap = maps[currentlyPreviewedIndex];
            if (oldMap.previewObject != null)
            {
                if (useLayerCulling) SetLayerRecursively(oldMap.previewObject, LayerMask.NameToLayer(hiddenLayerName));
                else oldMap.previewObject.SetActive(false);
            }
            if (oldMap.buttonText != null)
            {
                oldMap.buttonText.text = oldMap.originalTextString;
                oldMap.buttonText.color = oldMap.originalTextColor;
            }
        }
        currentlyPreviewedIndex = -1;
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

// =========================================================================
// INVISIBLE REPORTER SCRIPT
// This tiny script is automatically added to your physical buttons 
// by the Manager when the game starts. It just listens for hand collisions!
// =========================================================================
public class MapButtonTrigger : MonoBehaviour
{
    [HideInInspector] public MapSelectionManager manager;
    [HideInInspector] public int buttonIndex;

    private void OnTriggerEnter(Collider other)
    {
        if (manager == null) return;

        // STRICT CHECK: Only allow colliders with "hand" or "controller" in their name.
        // This prevents the player's main body capsule or other objects from triggering the button!
        string objName = other.name.ToLower();
        if (objName.Contains("hand") || objName.Contains("controller"))
        {
            manager.HandleButtonPress(buttonIndex);
        }
    }
}
