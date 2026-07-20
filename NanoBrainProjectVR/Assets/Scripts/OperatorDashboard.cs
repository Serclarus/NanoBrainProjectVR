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
    public List<string> mapNames = new List<string> { "MainMenu", "TrainingGrounds", "BoarHunting", "ShootingRange" };

    [Header("Testing")]
    [Tooltip("If true, playing in the Unity Editor will skip the PC Operator Dashboard and act like a VR headset.")]
    public bool forceVRInEditor = true;

    private Canvas dashboardCanvas;
    private UnityEngine.UI.Text connectionStatusText;
    private UnityEngine.UI.Text connectedClientsText;
    private UnityEngine.UI.Text spectatingText;
    private UnityEngine.UI.Text logTextUI;

    private void Awake()
    {
        // Keep this dashboard alive forever once created
        DontDestroyOnLoad(this.gameObject);
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Only show our specific custom logs on the UI to prevent spam
        if (logString.Contains("[NetworkPlayerLoadout]") || logString.Contains("[OperatorDashboard]") || logString.Contains("[VRSceneFader]"))
        {
            // Strip out rich text color tags for the UI so it looks clean
            string cleanLog = System.Text.RegularExpressions.Regex.Replace(logString, "<.*?>", string.Empty);
            LogToUI(cleanLog);
        }
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
        uiCam.backgroundColor = new Color(0.07f, 0.07f, 0.075f); // Modern dark mode background (near black)
        uiCam.cullingMask = 1 << 5; // UI Layer only
        
        // Setup Canvas
        GameObject canvasObj = new GameObject("OperatorDashboardCanvas");
        canvasObj.transform.SetParent(this.transform); // Ensure it survives DontDestroyOnLoad
        dashboardCanvas = canvasObj.AddComponent<Canvas>();
        dashboardCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        dashboardCanvas.worldCamera = uiCam;
        dashboardCanvas.sortingOrder = 999;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create a background panel
        GameObject panelObj = new GameObject("BackgroundPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = panelObj.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.12f, 0.13f, 0.95f); // Sleek dark gray panel

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
        CreateButton(panelObj.transform, "Load MainMenu", new Vector2(leftX, -130f), btnWidth, btnHeight, () => LoadMap("MainMenu"));
        CreateButton(panelObj.transform, "Load Training", new Vector2(rightX, -130f), btnWidth, btnHeight, () => LoadMap("TrainingGrounds"));

        // ROW 2: Map 3 (BoarHunting) and Map 4 (ShootingRange)
        CreateButton(panelObj.transform, "Load BoarHunting", new Vector2(leftX, -175f), btnWidth, btnHeight, () => LoadMap("BoarHunting"));
        CreateButton(panelObj.transform, "Load Range", new Vector2(rightX, -175f), btnWidth, btnHeight, () => LoadMap("ShootingRange"));

        // ROW 3: Reset Map and Pause Game
        CreateButton(panelObj.transform, "Reset Map", new Vector2(leftX, -220f), btnWidth, btnHeight, ResetCurrentMap);
        CreateButton(panelObj.transform, "Pause Game", new Vector2(rightX, -220f), btnWidth, btnHeight, TogglePause);

        // ROW 4: FORCE Spawn
        CreateButton(panelObj.transform, "FORCE Spawn", new Vector2(leftX, -275f), btnWidth, btnHeight, ForceSpawnWeaponsForVR);

        // ROW 5: Refill Mag and Load Shotgun
        CreateButton(panelObj.transform, "Refill Mag", new Vector2(leftX, -320f), btnWidth, btnHeight, RefillMags);
        CreateButton(panelObj.transform, "Load Shotgun", new Vector2(rightX, -320f), btnWidth, btnHeight, LoadShotgun);

        // RIGHT PANEL: Diagnostic Logs
        GameObject rightPanel = new GameObject("DiagnosticLogs");
        rightPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform rightRt = rightPanel.AddComponent<RectTransform>();
        rightRt.sizeDelta = new Vector2(400, 400);
        rightRt.anchoredPosition = new Vector2(300f, 0f);

        Image rightImg = rightPanel.AddComponent<Image>();
        rightImg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        logTextUI = CreateText(rightPanel.transform, "Waiting for network...", new Vector2(0f, 180f), 12, 380, 380);
        logTextUI.alignment = TextAnchor.UpperLeft;
        logTextUI.color = Color.green;

        // Fix: The UI Camera only renders Layer 5 (UI). All dynamically created objects default to Layer 0.
        // We must set the Canvas and all its children to Layer 5.
        SetLayerRecursively(canvasObj, 5);
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
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

    private bool hasRegisteredSceneListener = false;

    private void Update()
    {
        // Bulletproof listener registration. It doesn't rely on events, just waits until we are actively the Server AND the UI is ready!
        if (!hasRegisteredSceneListener && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
        {
            if (logTextUI != null) // Only execute if the UI has finished its 0.5s initialization, so we don't miss the log!
            {
                hasRegisteredSceneListener = true;
                Debug.Log("<color=magenta>[OperatorDashboard]</color> Actively running as Server. Registering ClientRequest_LoadScene listener!");
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("ClientRequest_LoadScene", OnClientRequestLoadScene);
            }
        }

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

        // CRITICAL FIX: The project uses the New Input System (activeInputHandler: 1).
        // Using StandaloneInputModule throws a fatal exception and completely breaks the UI!
        // We must ensure the scene has an EventSystem, and if not, we create one with the correct InputSystem UI module!
        var currentEventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (currentEventSystem == null)
        {
            Debug.LogWarning("[OperatorDashboard] No EventSystem found in scene! Creating one with InputSystemUIInputModule.");
            GameObject eventSystemObj = new GameObject("OperatorEventSystem");
            eventSystemObj.transform.SetParent(this.transform);
            currentEventSystem = eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
        else
        {
            // If an EventSystem exists but lacks the New Input System module, inject it!
            if (currentEventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null && 
                currentEventSystem.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>() == null)
            {
                currentEventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
    }

    private void LogToUI(string msg)
    {
        if (logTextUI != null)
        {
            // Keep logs to a reasonable length so it doesn't overflow
            string currentText = logTextUI.text;
            string[] lines = currentText.Split('\n');
            if (lines.Length > 20)
            {
                currentText = string.Join("\n", lines, 1, lines.Length - 1);
            }
            logTextUI.text = currentText + $"\n> {msg}";
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

    private void OnClientRequestLoadScene(ulong senderId, FastBufferReader messagePayload)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            messagePayload.ReadValueSafe(out Unity.Collections.FixedString32Bytes safeSceneName);
            string sceneName = safeSceneName.ToString();
            Debug.Log($"<color=magenta>[OperatorDashboard]</color> VR Client {senderId} requested to load scene: {sceneName}");
            LoadMap(sceneName);
        }
    }
}
