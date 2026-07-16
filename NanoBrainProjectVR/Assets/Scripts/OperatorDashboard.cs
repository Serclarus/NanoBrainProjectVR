using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using XRMultiplayer;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// PC Operator Dashboard — UI-ONLY controller.
/// Provides buttons for map loading, weapon management, and game control.
/// Does NOT create or manage any cameras. Spectator camera is handled entirely by NetworkPlayerLoadout.
/// </summary>
public class OperatorDashboard : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("The names of the scenes you want to be able to load from the dashboard")]
    public List<string> mapNames = new List<string> { "MainMenu", "TrainingGrounds", "BoarHunt" };

    [Header("Testing")]
    [Tooltip("If true, playing in the Unity Editor will skip the PC Operator Dashboard and act like a VR headset.")]
    public bool forceVRInEditor = true;

    private Canvas dashboardCanvas;
    private UnityEngine.UI.Text connectionStatusText;
    private UnityEngine.UI.Text connectedClientsText;
    private UnityEngine.UI.Text spectatingText;

    private void Awake()
    {
        // Keep this dashboard alive forever once created
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        StartCoroutine(InitializeDashboardRoutine());
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
        var xrDisplays = new List<UnityEngine.XR.XRDisplaySubsystem>();
        UnityEngine.SubsystemManager.GetSubsystems(xrDisplays);
        foreach (var display in xrDisplays)
        {
            if (display.running) isVRActive = true;
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

    private void BuildDashboardUI()
    {
        // Create a dedicated UI Camera so the screen isn't just a Unity warning
        GameObject camObj = new GameObject("OperatorUICamera");
        camObj.transform.SetParent(this.transform);
        Camera uiCam = camObj.AddComponent<Camera>();
        uiCam.clearFlags = CameraClearFlags.SolidColor;
        uiCam.backgroundColor = new Color(0.05f, 0.05f, 0.05f); // Very dark gray
        uiCam.cullingMask = 1 << 5; // UI Layer only
        
        // Setup Canvas
        GameObject canvasObj = new GameObject("OperatorDashboardCanvas");
        canvasObj.transform.SetParent(this.transform); // Ensure it survives DontDestroyOnLoad
        dashboardCanvas = canvasObj.AddComponent<Canvas>();
        dashboardCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        dashboardCanvas.worldCamera = uiCam;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create a background panel
        GameObject panelObj = new GameObject("BackgroundPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = panelObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);

        RectTransform panelRT = panelObj.GetComponent<RectTransform>();
        // Anchor to top-left of the screen
        panelRT.anchorMin = new Vector2(0, 1);
        panelRT.anchorMax = new Vector2(0, 1);
        panelRT.pivot = new Vector2(0, 1);
        panelRT.anchoredPosition = new Vector2(15, -15);
        panelRT.sizeDelta = new Vector2(300, 480);

        // Add a Title
        var titleTxt = CreateText(panelObj.transform, "OPERATOR DASHBOARD", new Vector2(0, -25), 18, 280, 40);
        titleTxt.fontStyle = FontStyle.Bold;

        // Add Connection Status Text
        connectionStatusText = CreateText(panelObj.transform, "Status: Waiting...", new Vector2(0, -60), 13, 280, 30);
        connectionStatusText.color = Color.yellow;
        
        connectedClientsText = CreateText(panelObj.transform, "Connected Clients: 0", new Vector2(0, -85), 13, 280, 30);
        connectedClientsText.color = Color.cyan;

        // Two columns of buttons
        float leftX = -70f;
        float rightX = 70f;
        float btnWidth = 130f;
        float btnHeight = 35f;
        
        // ROW 1: Map 1 (MainMenu) and Map 2 (TrainingGrounds)
        CreateButton(panelObj.transform, "Load MainMenu", new Vector2(leftX, -150f), btnWidth, btnHeight, () => LoadMap("MainMenu"));
        CreateButton(panelObj.transform, "Load Training", new Vector2(rightX, -150f), btnWidth, btnHeight, () => LoadMap("TrainingGrounds"));

        // ROW 2: Map 3 (BoarHunt) and Reset Map
        CreateButton(panelObj.transform, "Load BoarHunt", new Vector2(leftX, -195f), btnWidth, btnHeight, () => LoadMap("BoarHunt"));
        CreateButton(panelObj.transform, "Reset Map", new Vector2(rightX, -195f), btnWidth, btnHeight, ResetCurrentMap);

        // ROW 3: Pause Game and FORCE Spawn
        CreateButton(panelObj.transform, "Pause Game", new Vector2(leftX, -250f), btnWidth, btnHeight, TogglePause);
        CreateButton(panelObj.transform, "FORCE Spawn", new Vector2(rightX, -250f), btnWidth, btnHeight, ForceSpawnWeaponsForVR);

        // ROW 4: Refill Mag and Load Shotgun
        CreateButton(panelObj.transform, "Refill Mag", new Vector2(leftX, -295f), btnWidth, btnHeight, RefillMags);
        CreateButton(panelObj.transform, "Load Shotgun", new Vector2(rightX, -295f), btnWidth, btnHeight, LoadShotgun);
    }

    private void CreateButton(Transform parent, string buttonText, Vector2 anchoredPos, float width, float height, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = new GameObject($"Btn_{buttonText}");
        btnObj.transform.SetParent(parent, false);
        
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        CreateText(btnObj.transform, buttonText, Vector2.zero, 11, width, height);
    }

    private UnityEngine.UI.Text CreateText(Transform parent, string msg, Vector2 anchoredPos, int fontSize, float width = 250f, float height = 50f)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;

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
        // Update Connection Status UI
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

        // Update connected clients count
        if (connectedClientsText != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            int clientCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            connectedClientsText.text = $"Connected Clients: {clientCount}";
            connectedClientsText.color = clientCount > 1 ? Color.green : Color.yellow;
        }
    }

    // --- NETWORK COMMANDS ---

    private void LoadMap(string sceneName)
    {
        Debug.Log($"[OperatorDashboard] Requesting network load for map: {sceneName}");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
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

        var allLoadouts = FindObjectsByType<NetworkPlayerLoadout>(FindObjectsSortMode.None);
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

        Debug.Log($"[OperatorDashboard] Connected Client IDs: {string.Join(", ", NetworkManager.Singleton.ConnectedClientsIds)}");
    }
}
