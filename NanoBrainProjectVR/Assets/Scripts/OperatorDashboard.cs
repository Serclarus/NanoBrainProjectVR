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
    [Tooltip("The UI Prefab to spawn on the PC screen")]
    public GameObject dashboardUIPrefab;

    [Header("Testing")]
    [Tooltip("If true, playing in the Unity Editor will skip the PC Operator Dashboard and act like a VR headset.")]
    public bool forceVRInEditor = true;

    private Canvas dashboardCanvas;
    private UnityEngine.UI.Text connectionStatusText;
    private UnityEngine.UI.Text connectedClientsText;

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
        // Rebuild UI or refresh references if necessary on scene load
    }

    private IEnumerator InitializeDashboardRoutine()
    {
        // HARD GUARD: On Android (Meta Quest), this is ALWAYS a VR device. Never run operator dashboard.
        // Also skip it in the Unity Editor if forceVRInEditor is true.
        if (Application.platform == RuntimePlatform.Android || (Application.isEditor && forceVRInEditor))
        {
            Debug.Log("[OperatorDashboard] Running on Android/Quest or VR forced in Editor. Operator Dashboard disabled.");
            yield break;
        }

        // Wait a brief moment to ensure XR and Network systems have started
        yield return new WaitForSeconds(0.5f);

        // Determine if we are running in VR
        bool isVRActive = false;
        
        if (UnityEngine.XR.XRSettings.isDeviceActive) 
        {
            isVRActive = true;
        }
        else
        {
            var xrDisplays = new List<UnityEngine.XR.XRDisplaySubsystem>();
            UnityEngine.SubsystemManager.GetSubsystems(xrDisplays);
            foreach (var display in xrDisplays)
            {
                if (display.running) isVRActive = true;
            }
        }

        // If no VR headset is rendering, we assume this is the PC Operator!
        if (!isVRActive)
        {
            Debug.Log("[OperatorDashboard] No VR headset detected. Starting Operator Dashboard UI!");
            BuildDashboardUI();
        }
        else
        {
            Debug.Log("[OperatorDashboard] VR Headset detected. Operator Dashboard will remain hidden.");
        }
    }

    // CleanUpPCEnvironment and camera tracking removed to prevent VR tracking interference

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
        bgImage.color = new Color(0.1f, 0.1f, 0.12f, 0.85f); // Sleeker dark grey/blue color

        RectTransform panelRT = panelObj.GetComponent<RectTransform>();
        // Anchor to top-left of the screen and make it a clean, floating box rather than a full vertical strip!
        panelRT.anchorMin = new Vector2(0, 1);
        panelRT.anchorMax = new Vector2(0, 1);
        panelRT.pivot = new Vector2(0, 1);
        panelRT.anchoredPosition = new Vector2(15, -15);
        panelRT.sizeDelta = new Vector2(300, 480); // Width 300, Height 480

        // Add a Title
        var titleTxt = CreateText(panelObj.transform, "OPERATOR DASHBOARD", new Vector2(0, -25), 18, 280, 40);
        titleTxt.fontStyle = FontStyle.Bold;

        // Add Connection Status Text
        connectionStatusText = CreateText(panelObj.transform, "Status: Waiting...", new Vector2(0, -60), 13, 280, 30);
        connectionStatusText.color = Color.yellow;
        
        connectedClientsText = CreateText(panelObj.transform, "Connected Clients: 0", new Vector2(0, -85), 13, 280, 30);
        connectedClientsText.color = Color.cyan;

        // We create two columns of buttons below the headers!
        float leftX = -70f;
        float rightX = 70f;
        float btnWidth = 130f;
        float btnHeight = 35f;
        
        // ROW 1: Map 1 (MainMenu) and Map 2 (TrainingGrounds)
        CreateButton(panelObj.transform, "Load MainMenu", new Vector2(leftX, -130f), btnWidth, btnHeight, () => LoadMap("MainMenu"));
        CreateButton(panelObj.transform, "Load Training", new Vector2(rightX, -130f), btnWidth, btnHeight, () => LoadMap("TrainingGrounds"));

        // ROW 2: Map 3 (BoarHunt) and Reset Map
        CreateButton(panelObj.transform, "Load BoarHunt", new Vector2(leftX, -175f), btnWidth, btnHeight, () => LoadMap("BoarHunt"));
        CreateButton(panelObj.transform, "Reset Map", new Vector2(rightX, -175f), btnWidth, btnHeight, ResetCurrentMap);

        // ROW 3: Pause Game and FORCE Spawn
        CreateButton(panelObj.transform, "Pause Game", new Vector2(leftX, -230f), btnWidth, btnHeight, TogglePause);
        CreateButton(panelObj.transform, "FORCE Spawn", new Vector2(rightX, -230f), btnWidth, btnHeight, ForceSpawnWeaponsForVR);

        // ROW 4: Refill Mag and Load Shotgun
        CreateButton(panelObj.transform, "Refill Mag", new Vector2(leftX, -275f), btnWidth, btnHeight, RefillMags);
        CreateButton(panelObj.transform, "Load Shotgun", new Vector2(rightX, -275f), btnWidth, btnHeight, LoadShotgun);

        // Spectator label
        var specText = CreateText(panelObj.transform, "Spectating VR Player...", new Vector2(0, -330), 12, 280, 30);
        specText.color = Color.gray;
    }

    private void CreateButton(Transform parent, string buttonText, Vector2 anchoredPos, float width, float height, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = new GameObject($"Btn_{buttonText}");
        btnObj.transform.SetParent(parent, false);
        
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 1f); // darker slate-blue for premium look

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        var txt = CreateText(btnObj.transform, buttonText, Vector2.zero, 11, width, height);
    }

    private UnityEngine.UI.Text CreateText(Transform parent, string msg, Vector2 anchoredPos, int fontSize, float width = 250f, float height = 50f)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
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

        // 1b. Update connected clients count
        if (connectedClientsText != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            int clientCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            connectedClientsText.text = $"Connected Clients: {clientCount}";
            connectedClientsText.color = clientCount > 1 ? Color.green : Color.yellow;
        }

        // UI Updates only. Camera tracking logic removed completely.
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

    private void ForceSpawnWeaponsForVR()
    {
        Debug.Log("[OperatorDashboard] FORCE Spawn Weapons button pressed!");
        
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.LogError("[OperatorDashboard] Cannot force spawn — not connected to network!");
            return;
        }

        // Find ALL NetworkPlayerLoadout instances in the scene
        var allLoadouts = FindObjectsOfType<NetworkPlayerLoadout>();
        Debug.Log($"[OperatorDashboard] Found {allLoadouts.Length} NetworkPlayerLoadout instances.");

        foreach (var loadout in allLoadouts)
        {
            var netObj = loadout.GetComponent<NetworkObject>();
            if (netObj == null) continue;

            Debug.Log($"[OperatorDashboard] Loadout on '{loadout.gameObject.name}': " +
                      $"OwnerClientId={netObj.OwnerClientId}, " +
                      $"IsOwner={netObj.IsOwner}, " +
                      $"IsSpawned={netObj.IsSpawned}");

            // Force spawn for EVERY loadout that isn't ours (the PC operator)
            // Our own loadout is the one where IsOwner=true on this PC
            if (!netObj.IsOwner)
            {
                Debug.Log($"[OperatorDashboard] Calling ForceSpawnForClient({netObj.OwnerClientId}) on remote player loadout...");
                loadout.ForceSpawnForClient(netObj.OwnerClientId);
            }
            else
            {
                Debug.Log($"[OperatorDashboard] Skipping our own loadout (PC Operator).");
            }
        }

        // Also log all connected client IDs for diagnostics
        Debug.Log($"[OperatorDashboard] Connected Client IDs: {string.Join(", ", NetworkManager.Singleton.ConnectedClientsIds)}");
    }
}
