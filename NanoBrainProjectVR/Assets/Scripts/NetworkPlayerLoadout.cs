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

    private string debugStatus = "Waiting for Spawn...";
    private bool hasSpawnedWeapons = false;
    private static List<GameObject> activeWeapons = new List<GameObject>();

    [Header("Sync Settings")]
    [Tooltip("If true, playing in the Unity Editor will always spawn weapons and behave like a VR headset, even without one plugged in.")]
    public bool forceVRInEditor = true;
    public NetworkVariable<bool> isVRUser = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private bool isInitialized = false;

    public override void OnNetworkSpawn()
    {
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

        InitializePlayer();
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

                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
                }
            }
            else
            {
                // PC Operator: completely gut this avatar — it becomes nothing but a floating camera
                SetupAsGhostSpectator();
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

    /// <summary>
    /// Turns the PC operator's player prefab into a pure ghost camera.
    /// Everything is disabled/destroyed except one camera that becomes the spectator view.
    /// The avatar cannot interact with anything — it is just a camera floating in space.
    /// </summary>
    private void SetupAsGhostSpectator()
    {
        debugStatus = $"Ghost Spectator (Client {OwnerClientId}) — camera only";
        Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> {debugStatus}");

        // 1. Find the camera we'll keep alive as the spectator view
        Camera spectatorCamera = GetComponentInChildren<Camera>(true);
        if (spectatorCamera != null)
        {
            // Make sure it's active and rendering
            spectatorCamera.gameObject.SetActive(true);
            spectatorCamera.enabled = true;
            spectatorCamera.stereoTargetEye = StereoTargetEyeMask.None; // Force flat screen rendering
            
            // Add AudioListener if missing so the PC operator can hear the game
            if (spectatorCamera.GetComponent<AudioListener>() == null)
            {
                spectatorCamera.gameObject.AddComponent<AudioListener>();
            }

            // Tag it and name it
            spectatorCamera.gameObject.name = "SpectatorCamera";
            try
            {
                spectatorCamera.gameObject.tag = "SpectatorCamera";
            }
            catch (UnityException)
            {
                Debug.LogWarning("<color=yellow>[NetworkPlayerLoadout]</color> Tag 'SpectatorCamera' is not defined in the project. Using name fallback.");
            }

            // Unparent it so it can move freely
            spectatorCamera.transform.SetParent(null);

            // Add the new Sync script
            if (spectatorCamera.GetComponent<SpectatorCameraSync>() == null)
            {
                spectatorCamera.gameObject.AddComponent<SpectatorCameraSync>();
            }

            Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Spectator camera detached and configured!");
        }
        else
        {
            Debug.LogWarning("<color=red>[NetworkPlayerLoadout]</color> No camera found on player prefab for spectator view!");
        }

        // 2. Destroy the XR Device Simulator so it doesn't hijack mouse/keyboard on PC
        var simulator = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
        if (simulator != null) Destroy(simulator.gameObject);

        // 3. Disable ALL interaction components — the ghost cannot touch anything
        foreach (var interactor in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true))
            interactor.enabled = false;
        foreach (var controller in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>(true))
            controller.enabled = false;
        foreach (var manager in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>(true))
            manager.enabled = false;

        // 4. Disable ALL colliders — the ghost has no physics presence
        foreach (var col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // 5. Disable ALL renderers — the ghost is invisible
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
            rend.enabled = false;

        // 6. Disable ALL Rigidbodies — no physics simulation
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // 7. Disable CharacterController if present
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // 8. Disable the XR Origin so it doesn't fight with anything
        var xrOrigin = GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true);
        if (xrOrigin != null) xrOrigin.enabled = false;

        // 9. Disable BodyFollower — no body to follow
        var bodyFollower = GetComponentInChildren<BodyFollower>(true);
        if (bodyFollower != null) bodyFollower.enabled = false;

        // 10. Remove any extra AudioListeners left on the avatar body
        foreach (var listener in GetComponentsInChildren<AudioListener>(true))
        {
            Destroy(listener);
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
            Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Destroying remote camera to prevent hijack: {cam.gameObject.name}");
            Destroy(cam.gameObject); 
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
        }
        isVRUser.OnValueChanged -= OnVRUserChanged;
    }

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Scene loaded: {sceneName}. Teleporting and respawning weapons.");
        
        // Teleport to the spawn point in the new scene first
        TeleportToSpawnPoint();

        // When a new scene loads, the old weapons were destroyed. Respawn them!
        if (isVRUser.Value)
        {
            SpawnWeaponsForClient(OwnerClientId);
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
            // Spawn slightly above the player to prevent clipping into the floor
            Vector3 spawnPos = transform.position + (Vector3.up * 1.5f);
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
}
