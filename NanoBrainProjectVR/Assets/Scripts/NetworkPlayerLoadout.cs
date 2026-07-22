using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NetworkPlayerLoadout : NetworkBehaviour
{
    [Header("Weapon Prefabs (MUST be in NetworkManager list!)")]
    public GameObject shotgunPrefab;
    public GameObject riflePrefab;
    public GameObject pistolPrefab;

    [Header("Spectator Tracking")]
    [Tooltip("Drag the networked transform that represents the player's head here (e.g., your VRHeadAnchor or HeadVisual). The PC Dashboard will track this object over the network.")]
    public Transform spectatorHeadSource;

    private string debugStatus = "Waiting for Spawn...";
    private bool hasSpawnedWeapons = false;
    private static List<GameObject> activeWeapons = new List<GameObject>();

    [Header("Sync Settings")]
    [Tooltip("If true, playing in the Unity Editor will always spawn weapons and behave like a VR headset, even without one plugged in.")]
    public bool forceVRInEditor = true;
    public NetworkVariable<bool> isVRUser = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<Unity.Collections.FixedString32Bytes> requestedScene = new NetworkVariable<Unity.Collections.FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private bool isInitialized = false;

    public override void OnNetworkSpawn()
    {
        // Register this on EVERYONE so the Server/Host can listen to direct connection messages
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("GlobalRequest_SceneChange", OnGlobalSceneChangeRequest);
        }

        if (IsServer)
        {
            requestedScene.OnValueChanged += OnSceneRequested;
        }
        
        // 1. If this is a pre-placed scene object, only the server should despawn/destroy it.
        // We must defer it to the end of the frame to prevent Netcode state corruption during spawn processing.
        if (IsServer && NetworkObject != null && NetworkObject.IsSceneObject == true)
        {
            StartCoroutine(DeferredDespawnRoutine());
            return;
        }
        else if (!IsServer && NetworkObject != null && NetworkObject.IsSceneObject == true)
        {
            // Clients ignore pre-placed scene objects on spawn because the server will despawn them shortly
            return;
        }

        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> OnNetworkSpawn fired!" +
                  $" GameObject: {gameObject.name}" +
                  $" IsOwner: {IsOwner}" +
                  $" OwnerClientId: {OwnerClientId}" +
                  $" Platform: {Application.platform}");

        if (IsOwner)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_TogglePause", OnTogglePauseReceived);
            
            // Listen to Scene Events to force drop items right before a scene change!
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            }
        }

        InitializePlayer();
    }

    // Merged into the main OnNetworkDespawn at the bottom

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // When ANY scene load begins across the network, immediately force drop all held items!
        if (sceneEvent.SceneEventType == SceneEventType.Load)
        {
            Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Network Scene Load detected! Forcing drop of all items...");
            ForceDropAllInteractables();
        }
    }



    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Gained ownership of {gameObject.name}!");
        InitializePlayer();
    }

    private void InitializePlayer()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (IsOwner)
        {
            // On Android (Meta Quest), we are ALWAYS VR. On PC, check for a running XR display.
            // If playing in the Editor and forceVRInEditor is true, force it to act like a VR device.
            bool isVR = (Application.platform == RuntimePlatform.Android) || (Application.isEditor && forceVRInEditor) || CheckIsVRActive();

            Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Owner detected as {(isVR ? "VR" : "PC Operator")}");

            isVRUser.Value = isVR;

            // Teleport local player to an available spawn point in the scene
            TeleportToSpawnPoint();

            if (isVR)
            {
                // VR user: spawn weapons
                debugStatus = $"Spawning weapons for Client {OwnerClientId}!";
                Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> {debugStatus}");
                SpawnWeaponsForClient(OwnerClientId);

                // Dynamically attach detailed diagnostics loop
                gameObject.AddComponent<VRLastResortDiagnostics>();
                Debug.Log("<color=green>[NetworkPlayerLoadout]</color> Dynamically attached VRLastResortDiagnostics to player.");

                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
                }
            }
            else
            {
                // PC Operator: Pure 2D dashboard mode. 
                // We completely destroy the avatar so it has ZERO physical presence or camera in the game.
                Debug.Log("<color=yellow>[NetworkPlayerLoadout]</color> PC Operator detected. Despawning avatar to remain in pure 2D Dashboard mode.");
                
                if (IsServer)
                {
                    NetworkObject.Despawn(true);
                }
                else
                {
                    // If they are somehow a client operator, ask server to despawn
                    // But typically PC operator is the Host.
                    Destroy(gameObject);
                }
            }
        }
        else
        {
            // We are looking at another peer's replicated avatar.
            // CRITICAL: Disable ALL XR interaction components on non-owner avatars.
            // If left active, their ghost interactors will grab objects and prevent the local player from releasing them.
            DisableRemoteInteractors();

            // If they are a PC Operator, also hide their renderers so they are invisible.
            isVRUser.OnValueChanged += OnVRUserChanged;
            if (!isVRUser.Value)
            {
                HideRemotePCAvatar();
            }
        }
    }

    private bool CheckIsVRActive()
    {
        var xrDisplays = new List<UnityEngine.XR.XRDisplaySubsystem>();
        UnityEngine.SubsystemManager.GetSubsystems(xrDisplays);
        foreach (var display in xrDisplays)
        {
            if (display.running) return true;
        }
        return false;
    }

    private void OnVRUserChanged(bool previousValue, bool newValue)
    {
        if (!newValue)
        {
            HideRemotePCAvatar();
        }
    }

    /// <summary>
    /// Called on ANY device to hide a remote PC Operator's replicated avatar.
    /// ONLY disables renderers and colliders — NEVER touches cameras or XR Origins,
    /// because on the VR headset those could belong to the local player.
    /// </summary>
    private void HideRemotePCAvatar()
    {
        Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Hiding remote PC avatar (Client {OwnerClientId}) - renderers and colliders off.");
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = false;
    }

    /// <summary>
    /// Disables all XR interaction components on a non-owner replicated avatar.
    /// Without this, the remote avatar's interactors act as ghost hands that grab objects
    /// and prevent the local VR player from releasing them.
    /// </summary>
    private void DisableRemoteInteractors()
    {
        Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Disabling XR interactors on remote avatar (Client {OwnerClientId})");

        // Disable all XR Interactors (Direct, Ray, Socket, Poke, etc.)
        foreach (var interactor in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true))
        {
            interactor.enabled = false;
        }

        // Disable all XR Controllers so they don't process input
        foreach (var controller in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>(true))
        {
            controller.enabled = false;
        }

        // Disable any XR Interaction Manager on this avatar
        foreach (var manager in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>(true))
        {
            manager.enabled = false;
        }

        // Disable the XR Origin so it doesn't fight with the local player's tracking
        var xrOrigin = GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true);
        if (xrOrigin != null) xrOrigin.enabled = false;

        // CRITICAL: Destroy ALL Cameras and AudioListeners on remote avatars!
        // If we don't do this, when a remote player spawns, their camera (which is at their waist/floor level) 
        // can hijack the local VR player's rendering, making it look like the VR player dropped to waist level!
        foreach (var cam in GetComponentsInChildren<Camera>(true))
        {
            Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Destroying remote camera component to prevent hijack: {cam.gameObject.name}");
            
            // CRITICAL FIX: Only destroy the Camera component, NOT the gameObject!
            // If we destroy the gameObject, the PC loses the VR player's networked head transform!
            Destroy(cam); 
        }
        
        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
        {
            Destroy(listener);
        }
    }

    private IEnumerator DeferredDespawnRoutine()
    {
        yield return new WaitForEndOfFrame();
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log($"<color=red>[NetworkPlayerLoadout]</color> Server despawning pre-placed scene player object {gameObject.name} to prevent duplication in multiplayer.");
            NetworkObject.Despawn(true);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            
            if (IsOwner)
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
            }
        }
        isVRUser.OnValueChanged -= OnVRUserChanged;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("GlobalRequest_SceneChange");
        }

        if (IsServer)
        {
            requestedScene.OnValueChanged -= OnSceneRequested;
        }

        if (IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_TogglePause");
        }
        
        base.OnNetworkDespawn();
    }

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Scene loaded: {sceneName}. Teleporting and respawning weapons.");
        
        // Teleport to the spawn point in the new scene first
        TeleportToSpawnPoint();

        if (IsOwner && isVRUser.Value)
        {
            StartCoroutine(RebuildInteractionManagerRoutine());
        }
    }

    private System.Collections.IEnumerator RebuildInteractionManagerRoutine()
    {
        // Wait for the new scene to fully settle
        yield return new UnityEngine.WaitForSeconds(0.5f);

        // Force refresh all input action managers in the scene to kick-start controller tracking
        var inputManagers = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (var iam in inputManagers)
        {
            if (iam != null && iam.enabled)
            {
                try
                {
                    iam.DisableInput();
                    iam.EnableInput();
                    Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Force-refreshed InputActionManager inputs: {iam.gameObject.name}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=red>[NetworkPlayerLoadout]</color> Failed to refresh InputActionManager '{iam.name}': {e.Message}");
                }
            }
        }

        // Force refresh all XRInputModalityManagers to re-detect controller tracking state
        var modalityManagers = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.XRInputModalityManager>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
        foreach (var xmm in modalityManagers)
        {
            if (xmm != null && xmm.enabled)
            {
                try
                {
                    xmm.enabled = false;
                    xmm.enabled = true;
                    Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Force-refreshed XRInputModalityManager: {xmm.gameObject.name}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=red>[NetworkPlayerLoadout]</color> Failed to refresh XRInputModalityManager '{xmm.name}': {e.Message}");
                }
            }
        }

        // Find the active interaction manager (preferring the one on the player, fallback to scene)
        var newManager = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>(true);
        if (newManager == null)
        {
            newManager = UnityEngine.Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        }

        if (newManager == null)
        {
            Debug.LogError("<color=red>[NetworkPlayerLoadout]</color> No XRInteractionManager found anywhere!");
            yield break;
        }

        Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Binding interactors and weapons to manager '{newManager.name}'");

        // Bind all interactors on the player
        var interactors = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true);
        foreach (var interactor in interactors)
        {
            if (interactor.interactionManager != newManager)
            {
                interactor.interactionManager = newManager;
            }
        }

        // Bind all persistent weapons
        var allInteractables = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(UnityEngine.FindObjectsSortMode.None);
        foreach (var interactable in allInteractables)
        {
            if (interactable.interactionManager != newManager)
            {
                interactable.interactionManager = newManager;
            }
        }

        // ── Re-socket weapons into holsters ──
        yield return null;
        var weapons = UnityEngine.Object.FindObjectsByType<WeaponAutoReturn>(UnityEngine.FindObjectsSortMode.None);
        foreach (var weapon in weapons)
        {
            if (weapon.IsOwner)
            {
                weapon.ForceReturnToSocket();
            }
        }
    }

    private void TeleportToSpawnPoint()
    {
        if (!IsOwner) return;

        var spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints.Length > 0)
        {
            // Sort by index, then pick a spawn point based on client ID so multiple players don't spawn inside each other
            System.Array.Sort(spawnPoints, (a, b) => a.spawnIndex.CompareTo(b.spawnIndex));
            var selectedSpawn = spawnPoints[(int)(OwnerClientId % (ulong)spawnPoints.Length)];

            Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Teleporting local player {gameObject.name} to Spawn Point {selectedSpawn.name} at {selectedSpawn.transform.position}");

            // Temporarily disable CharacterController (if active) to prevent it from resetting the position
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = selectedSpawn.transform.position;
            transform.rotation = selectedSpawn.transform.rotation;

            // Fix the 20cm height offset: snap the player perfectly to the floor after teleporting
            var snapToGround = GetComponent<SnapToGround>();
            if (snapToGround != null)
            {
                snapToGround.Snap();
            }

            if (cc != null) cc.enabled = true;

            // CRITICAL FIX: Because weapons use retainTransformParent in XRI, they are not children of the player in the Unity Hierarchy locally.
            // When the player teleports to the new spawn point, the weapons are left behind in the world!
            // We must explicitly teleport anything the player is currently holding in their holsters/sockets.
            var sockets = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(true);
            foreach (var socket in sockets)
            {
                if (socket.hasSelection && socket.interactablesSelected.Count > 0)
                {
                    var interactable = socket.interactablesSelected[0];
                    Transform attach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
                    interactable.transform.position = attach.position;
                    interactable.transform.rotation = attach.rotation;
                }
            }
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[NetworkPlayerLoadout]</color> No PlayerSpawnPoint found in the scene! Defaulting to current position: {transform.position}");
        }
    }

    public void ForceSpawnForClient(ulong clientId)
    {
        debugStatus = $"Forcing spawn for Client {clientId}";
        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> {debugStatus}");
        SpawnWeaponsForClient(clientId);
    }

    private void SpawnWeaponsForClient(ulong clientId)
    {
        if (hasSpawnedWeapons) 
        {
            Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Weapons already spawned for Client {clientId}. Skipping duplicate spawn.");
            return;
        }

        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> SpawnWeaponsForClient({clientId}) called." +
                  $" shotgun={(shotgunPrefab != null ? shotgunPrefab.name : "NULL")}" +
                  $" rifle={(riflePrefab != null ? riflePrefab.name : "NULL")}" +
                  $" pistol={(pistolPrefab != null ? pistolPrefab.name : "NULL")}");

        int count = 0;
        if (shotgunPrefab != null) { SpawnAndAssign(shotgunPrefab, clientId); count++; }
        else Debug.LogWarning("<color=red>[NetworkPlayerLoadout]</color> shotgunPrefab is NULL!");
        
        if (riflePrefab != null) { SpawnAndAssign(riflePrefab, clientId); count++; }
        else Debug.LogWarning("<color=red>[NetworkPlayerLoadout]</color> riflePrefab is NULL!");
        
        if (pistolPrefab != null) { SpawnAndAssign(pistolPrefab, clientId); count++; }
        else Debug.LogWarning("<color=red>[NetworkPlayerLoadout]</color> pistolPrefab is NULL!");

        Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Attempted to spawn {count}/3 weapons for Client {clientId}.");
        hasSpawnedWeapons = true;
    }

    private void SpawnAndAssign(GameObject prefab, ulong clientId)
    {
        try
        {
            // Spawn deep underground so the weapons are completely invisible to the player
            // while they wait for WeaponAutoReturn.cs to snap them into their holsters!
            Vector3 spawnPos = transform.position + (Vector3.down * 5.0f);
            Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Instantiating {prefab.name} at {spawnPos}...");
            
            GameObject wep = Instantiate(prefab, spawnPos, Quaternion.identity);
            NetworkObject netObj = wep.GetComponent<NetworkObject>();
            
            if (netObj != null)
            {
                // In Distributed Authority, the spawning client automatically gets ownership.
                // SpawnWithOwnership ensures the correct client owns the weapon in all topologies.
                netObj.SpawnWithOwnership(clientId);
                activeWeapons.Add(wep);
                debugStatus = $"Successfully spawned {prefab.name} for Client {clientId}";
                Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> {debugStatus} | NetworkObjectId: {netObj.NetworkObjectId}");
            }
            else
            {
                debugStatus = $"FAILED: {prefab.name} has no NetworkObject component!";
                Debug.LogError($"<color=red>[NetworkPlayerLoadout]</color> {debugStatus}");
                Destroy(wep);
            }
        }
        catch (System.Exception e)
        {
            debugStatus = $"EXCEPTION spawning {prefab.name}: {e.Message}";
            Debug.LogError($"<color=red>[NetworkPlayerLoadout]</color> {debugStatus}\n{e.StackTrace}");
        }
    }

    // --- NEW: SCENE CHANGE RPC ---
    [ServerRpc(RequireOwnership = false)]
    public void RequestSceneChangeServerRpc(string sceneName)
    {
        Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> VR Client requested scene change to: {sceneName}. Loading now...");
        
        if (NetworkManager.Singleton.SceneManager != null)
        {
            // The Server actually executes the scene change for everyone
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    // --- CUSTOM COMMAND RECEIVERS ---
    private void OnTogglePauseReceived(ulong senderId, FastBufferReader messagePayload)
    {
        Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Toggle Pause command received!");

        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
            if (VRSceneFader.Instance != null) VRSceneFader.Instance.PausedVignette(false);
            Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Game Resumed.");
        }
        else
        {
            Time.timeScale = 0f;
            if (VRSceneFader.Instance != null) VRSceneFader.Instance.PausedVignette(true);
            Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Game Paused.");

            // Force drop held weapons so they auto-return to holsters!
            ForceDropAllInteractables();
        }
    }

    private void ForceDropAllInteractables()
    {
        var grabInteractables = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(FindObjectsSortMode.None);
        foreach (var grab in grabInteractables)
        {
            if (grab != null && grab.isSelected && grab.interactorsSelecting.Count > 0)
            {
                // Must iterate backward when modifying collections or cancelling selections
                for (int i = grab.interactorsSelecting.Count - 1; i >= 0; i--)
                {
                    var interactor = grab.interactorsSelecting[i];
                    
                    // Exclude sockets so magazines don't fall out of guns, and attachments don't break!
                    if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) continue;
                    
                    if (grab.interactionManager != null)
                    {
                        grab.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
                        Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Forced player to drop object: {grab.gameObject.name}");
                    }
                }
            }
        }
    }

    private void OnSceneRequested(Unity.Collections.FixedString32Bytes previousValue, Unity.Collections.FixedString32Bytes newValue)
    {
        if (IsServer && !string.IsNullOrEmpty(newValue.ToString()))
        {
            Debug.Log($"<color=magenta>[NetworkPlayerLoadout]</color> VR Client requested scene change to: {newValue.ToString()} via NetworkVariable! Executing...");
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(newValue.ToString(), UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            requestedScene.Value = ""; // Reset
        }
    }

    // THIS METHOD RUNS ON THE PC HOST VIA DIRECT MESSAGING EXTRACTION
    private void OnGlobalSceneChangeRequest(ulong senderId, FastBufferReader messagePayload)
    {
        messagePayload.ReadValueSafe(out Unity.Collections.FixedString32Bytes sceneNameBytes);
        string sceneToLoad = sceneNameBytes.ToString();

        Debug.Log($"<color=magenta>[NetworkPlayerLoadout]</color> CRITICAL: Bypassed scene-sync constraint! Received direct scene change request for: {sceneToLoad}");

        bool isListen = NetworkManager.Singleton.IsListening;
        bool hasSceneMgr = (NetworkManager.Singleton.SceneManager != null);
        Debug.Log($"<color=orange>[NetworkPlayerLoadout]</color> Checking conditions -> Singleton.IsListening: {isListen}, HasSceneManager: {hasSceneMgr}");

        // Use the EXACT same condition that OperatorDashboard uses successfully
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.SceneManager != null)
        {
            var status = NetworkManager.Singleton.SceneManager.LoadScene(sceneToLoad, UnityEngine.SceneManagement.LoadSceneMode.Single);
            Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> LoadScene result for {sceneToLoad}: {status}");
        }
    }
}

public class VRLastResortDiagnostics : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(DiagnosticLoop());
    }

    private System.Collections.IEnumerator DiagnosticLoop()
    {
        Debug.Log("<color=cyan>[VRDiagnostics]</color> Started detailed VR diagnostics loop.");
        yield return new UnityEngine.WaitForSeconds(2f); // Wait for scene to settle

        while (true)
        {
            try
            {
                RunDiagnostics();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[VRDiagnostics]</color> Exception during diagnostics: {e.Message}\n{e.StackTrace}");
            }
            yield return new UnityEngine.WaitForSeconds(3f); // Repeat every 3 seconds
        }
    }

    private void RunDiagnostics()
    {
        Debug.Log("<color=yellow>================= VR RUNTIME DIAGNOSTICS =================</color>");

        // 1. Check Active XRInteractionManager
        var managers = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[Manager Info] Found {managers.Length} XRInteractionManager(s) in scene:");
        foreach (var mgr in managers)
        {
            Debug.Log($" - Manager Name: '{mgr.gameObject.name}' | Active: {mgr.gameObject.activeInHierarchy} | Enabled: {mgr.enabled} | Scene: {mgr.gameObject.scene.name}");
        }

        var activeManager = Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
        if (activeManager == null)
        {
            Debug.LogError("[Manager Info] CRITICAL: No active XRInteractionManager found!");
            return;
        }

        // 2. Query Manager's registered list
        var registeredInteractors = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor>();
        activeManager.GetRegisteredInteractors(registeredInteractors);
        Debug.Log($"[Manager Registrations] Registered Interactors in '{activeManager.name}' ({registeredInteractors.Count}):");
        foreach (var ri in registeredInteractors)
        {
            if (ri is MonoBehaviour mb)
            {
                Debug.Log($"   * Interactor: '{mb.gameObject.name}' | Type: {ri.GetType().Name} | Enabled: {mb.enabled}");
            }
        }

        var registeredInteractables = new System.Collections.Generic.List<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>();
        activeManager.GetRegisteredInteractables(registeredInteractables);
        Debug.Log($"[Manager Registrations] Registered Interactables in '{activeManager.name}' ({registeredInteractables.Count}):");
        foreach (var ri in registeredInteractables)
        {
            if (ri is MonoBehaviour mb)
            {
                Debug.Log($"   * Interactable: '{mb.gameObject.name}' | Type: {ri.GetType().Name} | Enabled: {mb.enabled}");
            }
        }

        // 3. Inspect Local Interactors (Hands/Rays/Sockets on this player)
        var playerInteractors = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true);
        Debug.Log($"[Local Player Interactors] Found {playerInteractors.Length} interactors on local player rig:");
        foreach (var interactor in playerInteractors)
        {
            string managerStr = interactor.interactionManager != null ? interactor.interactionManager.gameObject.name : "NULL";
            bool isRegistered = registeredInteractors.Contains(interactor);
            
            // Check targets
            string hoverTargetsStr = "None";
            if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.IXRHoverInteractor hoverInteractor)
            {
                var hovers = hoverInteractor.interactablesHovered;
                if (hovers != null && hovers.Count > 0)
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var h in hovers)
                    {
                        if (h is MonoBehaviour mb) names.Add(mb.gameObject.name);
                    }
                    hoverTargetsStr = string.Join(", ", names);
                }
            }

            string selectTargetsStr = "None";
            if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor selectInteractor)
            {
                var selects = selectInteractor.interactablesSelected;
                if (selects != null && selects.Count > 0)
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var s in selects)
                    {
                        if (s is MonoBehaviour mb) names.Add(mb.gameObject.name);
                    }
                    selectTargetsStr = string.Join(", ", names);
                }
            }

            Debug.Log($"   * Name: '{interactor.gameObject.name}' | Type: {interactor.GetType().Name}\n" +
                      $"     Enabled: {interactor.enabled} | Assigned Manager: '{managerStr}' | Registered: {isRegistered}\n" +
                      $"     Hovering: [{hoverTargetsStr}] | Selecting: [{selectTargetsStr}]\n" +
                      $"     Local Position: {interactor.transform.localPosition} | Local Rotation: {interactor.transform.localRotation.eulerAngles}");

            // Additional Ray Interactor specific check
            if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor)
            {
                if (rayInteractor.TryGetHitInfo(out Vector3 hitPosition, out Vector3 hitNormal, out int positionInLine, out bool isValidTarget))
                {
                    Debug.Log($"     [Ray Interactor] Ray is HIT! Target position: {hitPosition} | Valid Target: {isValidTarget}");
                }
                else
                {
                    Debug.Log("     [Ray Interactor] Ray is NOT hitting any physics colliders.");
                }
            }
        }

        // 4. Check global inputs & actions
        Debug.Log("[Input Tracking Check]");
        var inputManagers = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var iam in inputManagers)
        {
            Debug.Log($" - InputActionManager: '{iam.gameObject.name}' | Enabled: {iam.enabled}");
            if (iam.actionAssets != null)
            {
                foreach (var asset in iam.actionAssets)
                {
                    Debug.Log($"   * Action Asset: '{asset.name}' | Enabled: {asset.enabled}");
                }
            }
        }

        Debug.Log("<color=yellow>==========================================================</color>");
    }
}

