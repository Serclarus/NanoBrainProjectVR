using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using XRMultiplayer;
using System.Collections;
using System.Collections.Generic;

public class OperatorDashboard : MonoBehaviour
{
    [Header("Spectator Settings")]
    [Tooltip("If true, the PC camera will lock to the VR player's head")]
    public bool enableSpectatorCamera = true;
    
    [Header("UI Settings")]
    [Tooltip("The names of the scenes you want to be able to load from the dashboard")]
    public List<string> mapNames = new List<string> { "MainMenu", "TrainingGrounds", "BoarHunt" };

    private Canvas dashboardCanvas;
    private Camera pcCamera;
    private Transform vrTargetHead;
    private UnityEngine.UI.Text connectionStatusText;

    private void Awake()
    {
        // Keep this dashboard alive forever once created
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        StartCoroutine(InitializeDashboardRoutine());
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // The VR player's body is destroyed and recreated on scene loads, so we must find their new head!
        vrTargetHead = null;

        if (pcCamera != null)
        {
            CleanUpPCEnvironment();
        }
    }

    private IEnumerator InitializeDashboardRoutine()
    {
        // Wait a brief moment to ensure XR and Network systems have started
        yield return new WaitForSeconds(0.5f);

        // Determine if we are running in VR
        bool isVRActive = false;
        var xrDisplays = new List<UnityEngine.XR.XRDisplaySubsystem>();
        UnityEngine.SubsystemManager.GetSubsystems(xrDisplays);
        foreach (var display in xrDisplays)
        {
            if (display.running) isVRActive = true;
        }

        // If no VR headset is rendering, we assume this is the PC Operator!
        if (!isVRActive)
        {
            Debug.Log("[OperatorDashboard] No VR headset detected. Starting Operator Dashboard!");
            SetupPCEnvironment();
        }
        else
        {
            Debug.Log("[OperatorDashboard] VR Headset detected. Operator Dashboard will remain hidden.");
        }
    }

    private void SetupPCEnvironment()
    {
        // 1. Create a dedicated PC Spectator Camera (DO NOT hijack Camera.main as it belongs to XR Origin)
        GameObject camObj = new GameObject("OperatorSpectatorCamera");
        camObj.transform.SetParent(this.transform); // Ensure it survives DontDestroyOnLoad
        pcCamera = camObj.AddComponent<Camera>();
        camObj.tag = "MainCamera";

        // Copy all settings (Culling Mask, Skybox, FOV) from the original VR Camera before we disable it
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            pcCamera.CopyFrom(mainCam);
        }

        // Add AudioListener so the PC Operator can hear the game audio
        if (pcCamera.GetComponent<AudioListener>() == null)
        {
            pcCamera.gameObject.AddComponent<AudioListener>();
        }

        // Disable VR rendering for the PC camera to save performance and prevent bugs
        pcCamera.stereoTargetEye = StereoTargetEyeMask.None;

        // Force this to be the highest priority camera
        pcCamera.depth = 99;
        
        CleanUpPCEnvironment();

        // 2. Build the UI Dashboard automatically
        BuildDashboardUI();
    }

    private void CleanUpPCEnvironment()
    {
        // Destroy the XR Device Simulator so it doesn't hijack mouse/keyboard
        var simulator = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
        if (simulator != null) Destroy(simulator.gameObject);

        // Find ALL XR Origins in the scene
        var xrOrigins = FindObjectsOfType<Unity.XR.CoreUtils.XROrigin>();
        foreach (var xrOrigin in xrOrigins)
        {
            var netObj = xrOrigin.GetComponentInParent<Unity.Netcode.NetworkObject>();
            
            // If it has no NetworkObject, it's a scene-default offline rig. Disable it.
            if (netObj == null)
            {
                xrOrigin.gameObject.SetActive(false);
            }
            // If it has a NetworkObject and belongs to US (the PC Host), disable it so we are an invisible spectator.
            else if (netObj.IsOwner)
            {
                xrOrigin.gameObject.SetActive(false);
            }
            // If it belongs to a REMOTE client (the VR player), DO NOT disable it! We need to see them!
        }
    }

    private void BuildDashboardUI()
    {
        GameObject canvasObj = new GameObject("OperatorDashboardCanvas");
        canvasObj.transform.SetParent(this.transform); // Ensure it survives DontDestroyOnLoad
        dashboardCanvas = canvasObj.AddComponent<Canvas>();
        dashboardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create a background panel
        GameObject panelObj = new GameObject("BackgroundPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = panelObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f); // Semi-transparent black

        RectTransform panelRT = panelObj.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0, 0);
        panelRT.anchorMax = new Vector2(0.3f, 1); // Takes up the left 30% of the screen
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Add a Title
        CreateText(panelObj.transform, "OPERATOR DASHBOARD", new Vector2(0, 200), 24);

        // Add Connection Status Text
        connectionStatusText = CreateText(panelObj.transform, "Status: Waiting...", new Vector2(0, 160), 18);
        connectionStatusText.color = Color.yellow;

        // Create Map Loading Buttons
        float yOffset = 100f;
        foreach (string mapName in mapNames)
        {
            CreateButton(panelObj.transform, $"Load {mapName}", new Vector2(0, yOffset), () => LoadMap(mapName));
            yOffset -= 60f;
        }

        // Create Game Action Buttons
        CreateButton(panelObj.transform, "Toggle Pause Game", new Vector2(0, yOffset - 20f), TogglePause);
        CreateButton(panelObj.transform, "Refill Held Mag", new Vector2(0, yOffset - 70f), RefillMags);
        CreateButton(panelObj.transform, "Load Held Shotgun", new Vector2(0, yOffset - 120f), LoadShotgun);
        CreateButton(panelObj.transform, "Reset Current Map", new Vector2(0, yOffset - 170f), ResetCurrentMap);
        
        CreateText(panelObj.transform, "Spectating VR Player...", new Vector2(0, -350), 16);
    }

    private void CreateButton(Transform parent, string buttonText, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = new GameObject($"Btn_{buttonText}");
        btnObj.transform.SetParent(parent, false);
        
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 40);
        rt.anchoredPosition = anchoredPos;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        CreateText(btnObj.transform, buttonText, Vector2.zero, 18);
    }

    private UnityEngine.UI.Text CreateText(Transform parent, string msg, Vector2 anchoredPos, int fontSize)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(250, 50);
        rt.anchoredPosition = anchoredPos;

        // Using standard Unity Text to avoid TextMeshPro font asset missing errors in pure scripts
        UnityEngine.UI.Text txt = textObj.AddComponent<UnityEngine.UI.Text>();
        txt.text = msg;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = fontSize;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        
        return txt;
    }

    private void Update()
    {
        // 1. Update Connection Status UI
        if (connectionStatusText != null && XRINetworkGameManager.Instance != null)
        {
            var state = XRINetworkGameManager.CurrentConnectionState.Value;
            connectionStatusText.text = $"Status: {state}";
            
            if (state == XRINetworkGameManager.ConnectionState.Connected)
                connectionStatusText.color = Color.green;
            else if (state == XRINetworkGameManager.ConnectionState.Connecting)
                connectionStatusText.color = Color.yellow;
            else
                connectionStatusText.color = Color.red;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !enableSpectatorCamera || pcCamera == null) return;

        // 3. Find the VR player's head over the network
        if (vrTargetHead == null)
        {
            // We want to spectate the VR player.
            // If the PC is the Host, the VR player is a Client. If the PC is a Client, the VR player is the Host (or another client).
            // So we just look for ANY player that is NOT the local PC player.
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId != NetworkManager.Singleton.LocalClientId) // Don't spectate yourself
                {
                    if (client.PlayerObject != null)
                    {
                        // 1. Try finding an actual Camera component (unlikely on network avatars, but possible)
                        Camera cam = client.PlayerObject.GetComponentInChildren<Camera>();
                        if (cam != null)
                        {
                            vrTargetHead = cam.transform;
                        }
                        else
                        {
                            // 2. Try finding the standard XR Origin 'Main Camera' transform
                            Transform mainCam = client.PlayerObject.transform.Find("Camera Offset/Main Camera") ?? client.PlayerObject.transform.Find("Main Camera");
                            if (mainCam != null)
                            {
                                vrTargetHead = mainCam;
                            }
                            else
                            {
                                // 3. Try finding a generic 'Head' transform
                                Transform head = client.PlayerObject.transform.Find("Head") ?? client.PlayerObject.transform.Find("Camera Offset/Head");
                                if (head != null)
                                {
                                    vrTargetHead = head;
                                }
                                else
                                {
                                    // 4. Fallback to the root player object
                                    vrTargetHead = client.PlayerObject.transform;
                                }
                            }
                        }
                        Debug.Log($"[OperatorDashboard] Found VR Player! Locked Spectator Camera to: {vrTargetHead.name}");
                        break;
                    }
                }
            }
        }

        // 4. Lock the PC Camera to the VR Head
        if (vrTargetHead != null)
        {
            pcCamera.transform.position = vrTargetHead.position;
            
            // If we fell back to the root transform (which has the NetworkObject), add a fake head height offset so we aren't looking at the floor
            if (vrTargetHead.GetComponent<Unity.Netcode.NetworkObject>() != null)
            {
                pcCamera.transform.position += Vector3.up * 1.6f;
            }

            pcCamera.transform.rotation = vrTargetHead.rotation;
        }
    }

    // --- NETWORK COMMANDS ---

    private void LoadMap(string sceneName)
    {
        Debug.Log($"[OperatorDashboard] Requesting network load for map: {sceneName}");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // This forces all clients (including the VR headset) to load the new map synchronously!
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void ResetCurrentMap()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        LoadMap(currentScene);
    }

    private void TogglePause()
    {
        Debug.Log("[OperatorDashboard] Sending Toggle Pause command...");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("OperatorCommand_TogglePause", NetworkManager.ServerClientId, new FastBufferWriter(0, Unity.Collections.Allocator.Temp), NetworkDelivery.Reliable);
            // Also send to all clients (the VR headset)
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("OperatorCommand_TogglePause", new FastBufferWriter(0, Unity.Collections.Allocator.Temp), NetworkDelivery.Reliable);
        }
    }

    private void RefillMags()
    {
        Debug.Log("[OperatorDashboard] Sending Refill Mags command...");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("OperatorCommand_RefillMags", new FastBufferWriter(0, Unity.Collections.Allocator.Temp), NetworkDelivery.Reliable);
        }
    }

    private void LoadShotgun()
    {
        Debug.Log("[OperatorDashboard] Sending Load Shotgun command...");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("OperatorCommand_LoadShotgun", new FastBufferWriter(0, Unity.Collections.Allocator.Temp), NetworkDelivery.Reliable);
        }
    }
}
