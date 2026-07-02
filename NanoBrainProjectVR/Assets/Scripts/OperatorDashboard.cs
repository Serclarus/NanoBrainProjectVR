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

    private void Awake()
    {
        // Keep this dashboard alive forever once created
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        // Only run this system on the server/host (the PC)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            SetupPCEnvironment();
        }
    }

    private void SetupPCEnvironment()
    {
        // 1. Find or create the PC Camera
        pcCamera = Camera.main;
        if (pcCamera == null)
        {
            GameObject camObj = new GameObject("OperatorSpectatorCamera");
            pcCamera = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        // Disable VR rendering for the PC camera to save performance and prevent bugs
        pcCamera.stereoTargetEye = StereoTargetEyeMask.None;

        // 2. Build the UI Dashboard automatically
        BuildDashboardUI();
    }

    private void BuildDashboardUI()
    {
        GameObject canvasObj = new GameObject("OperatorDashboardCanvas");
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

        // Create Map Loading Buttons
        float yOffset = 100f;
        foreach (string mapName in mapNames)
        {
            CreateButton(panelObj.transform, $"Load {mapName}", new Vector2(0, yOffset), () => LoadMap(mapName));
            yOffset -= 60f;
        }

        // Create Game Action Buttons
        CreateButton(panelObj.transform, "Reset Current Map", new Vector2(0, yOffset - 40f), ResetCurrentMap);
        
        CreateText(panelObj.transform, "Spectating VR Player...", new Vector2(0, -300), 16);
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

    private void CreateText(Transform parent, string msg, Vector2 anchoredPos, int fontSize)
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
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || !enableSpectatorCamera || pcCamera == null) return;

        // 3. Find the VR player's head over the network
        if (vrTargetHead == null)
        {
            // Specifically look for the Client's player avatar, NOT the local PC host avatar!
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.ClientId != NetworkManager.Singleton.LocalClientId) // Don't spectate yourself
                {
                    XRINetworkPlayer vrPlayer = client.PlayerObject.GetComponent<XRINetworkPlayer>();
                    if (vrPlayer != null && vrPlayer.head != null)
                    {
                        vrTargetHead = vrPlayer.head;
                        Debug.Log("[OperatorDashboard] Found VR Player Head! Locking Spectator Camera.");
                        break;
                    }
                }
            }
        }

        // 4. Lock the PC Camera to the VR Head
        if (vrTargetHead != null)
        {
            pcCamera.transform.position = vrTargetHead.position;
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
}
