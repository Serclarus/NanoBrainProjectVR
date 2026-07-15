using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class NetworkPlayerLoadout : NetworkBehaviour
{
    [Header("Weapon Prefabs (MUST be in NetworkManager list!)")]
    public GameObject shotgunPrefab;
    public GameObject riflePrefab;
    public GameObject pistolPrefab;

    private string debugStatus = "Waiting for Spawn...";
    private bool hasSpawnedWeapons = false;
    private static System.Collections.Generic.List<GameObject> activeWeapons = new System.Collections.Generic.List<GameObject>();

    [Header("Sync Settings")]
    public NetworkVariable<bool> isVRUser = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Vector3> vrHeadPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Quaternion> vrHeadRotation = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Transform localHeadTransform;

    private void Update()
    {
        if (IsOwner && isVRUser.Value)
        {
            if (localHeadTransform == null)
            {
                localHeadTransform = transform.Find("Camera Offset/Main Camera") ??
                                     transform.Find("Main Camera") ??
                                     transform.Find("Head") ??
                                     transform.Find("Camera Offset/Head") ??
                                     GetComponentInChildren<Camera>()?.transform;
            }

            if (localHeadTransform != null)
            {
                vrHeadPosition.Value = localHeadTransform.position;
                vrHeadRotation.Value = localHeadTransform.rotation;
            }
        }
    }

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
            bool isVR = (Application.platform == RuntimePlatform.Android) || CheckIsVRActive();

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
                // PC Operator: hide our own avatar (safe because we are on PC, not VR)
                HideAvatarOnPC();
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
        var xrDisplays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
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
    /// Called ONLY on the PC itself to hide its own avatar. 
    /// Safe to disable cameras/XR Origins here because the PC has its own OperatorDashboard spectator camera.
    /// </summary>
    private void HideAvatarOnPC()
    {
        debugStatus = $"Hidden PC Operator avatar (Client {OwnerClientId})";
        Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> {debugStatus}");

        foreach (var cam in GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;

        var xrOrigin = GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true);
        if (xrOrigin != null) xrOrigin.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called on ANY device to hide a remote PC Operator's replicated avatar.
    /// ONLY disables renderers — NEVER touches cameras or XR Origins,
    /// because on the VR headset those could belong to the local player.
    /// </summary>
    private void HideRemotePCAvatar()
    {
        Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> Hiding remote PC avatar (Client {OwnerClientId}) - renderers only.");
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
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
    }

    private System.Collections.IEnumerator DeferredDespawnRoutine()
    {
        yield return new WaitForEndOfFrame();
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log($"<color=red>[NetworkPlayerLoadout]</color> Server despawning pre-placed scene player object {gameObject.name} to prevent duplication in multiplayer.");
            NetworkObject.Despawn(true);
        }
    }

    private void OnGUI()
    {
        // Show diagnostics on the owner's screen (VR or PC)
        if (!IsOwner) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20; // Slightly smaller to fit more info
        style.normal.textColor = Color.cyan;

        GUILayout.BeginArea(new Rect(10, 10, 950, 950));
        GUILayout.Label($"--- NETWORK PLAYER DIAGNOSTICS ---", style);
        GUILayout.Label($"My ClientId: {OwnerClientId} | IsOwner: {IsOwner} | IsServer: {IsServer}", style);
        GUILayout.Label($"Status: {debugStatus}", style);
        
        // 1. Get XROrigin info
        var xrOrigin = GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true);
        if (xrOrigin != null)
        {
            GUILayout.Label($"[XR Origin] Found on: {xrOrigin.gameObject.name}", style);
            GUILayout.Label($"  - Tracking Mode: {xrOrigin.RequestedTrackingOriginMode} (Current: {xrOrigin.CurrentTrackingOriginMode})", style);
            GUILayout.Label($"  - CameraYOffset: {xrOrigin.CameraYOffset} meters", style);
            GUILayout.Label($"  - Origin Pos (World): {xrOrigin.transform.position}", style);
            if (xrOrigin.CameraFloorOffsetObject != null)
            {
                GUILayout.Label($"  - CameraOffset Pos (Local): {xrOrigin.CameraFloorOffsetObject.transform.localPosition}", style);
            }
            if (xrOrigin.Camera != null)
            {
                GUILayout.Label($"  - Camera Pos (World): {xrOrigin.Camera.transform.position} | Local: {xrOrigin.Camera.transform.localPosition}", style);
            }
        }
        else
        {
            GUILayout.Label($"[XR Origin] NOT FOUND in hierarchy!", style);
        }

        // 2. Camera.main info
        if (Camera.main != null)
        {
            GUILayout.Label($"[Camera.main] Active: {Camera.main.name} | Tag: {Camera.main.tag}", style);
            GUILayout.Label($"  - World Pos: {Camera.main.transform.position} | Local: {Camera.main.transform.localPosition}", style);
            GUILayout.Label($"  - Parent: {(Camera.main.transform.parent != null ? Camera.main.transform.parent.name : "None")}", style);
        }
        else
        {
            GUILayout.Label($"[Camera.main] NOT FOUND in scene!", style);
        }

        // 3. Hands tracking info
        GameObject leftHand = GameObject.Find("Left Controller");
        GameObject rightHand = GameObject.Find("Right Controller");
        GUILayout.Label($"[Left Controller] {(leftHand != null ? $"Found | World Pos: {leftHand.transform.position} | Local: {leftHand.transform.localPosition}" : "NOT FOUND")}", style);
        GUILayout.Label($"[Right Controller] {(rightHand != null ? $"Found | World Pos: {rightHand.transform.position} | Local: {rightHand.transform.localPosition}" : "NOT FOUND")}", style);

        // 4. Vest info
        var bodyFollower = GetComponentInChildren<BodyFollower>(true);
        if (bodyFollower != null)
        {
            GUILayout.Label($"[BodyFollower] Found on: {bodyFollower.gameObject.name}", style);
            GUILayout.Label($"  - Position: {bodyFollower.transform.position} | Height Offset: {bodyFollower.bodyHeightOffset}", style);
            GUILayout.Label($"  - Head Target: {(bodyFollower.head != null ? bodyFollower.head.name : "NULL")}", style);
        }

        activeWeapons.RemoveAll(w => w == null);
        GUILayout.Label($"Active Weapons: {activeWeapons.Count}", style);
        foreach (var w in activeWeapons)
        {
            GUILayout.Label($"  - {w.name} at {w.transform.position}", style);
        }
        GUILayout.EndArea();
    }


    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
        isVRUser.OnValueChanged -= OnVRUserChanged;
    }

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Scene loaded: {sceneName}. Teleporting and respawning weapons.");
        
        // Teleport to the spawn point in the new scene first
        TeleportToSpawnPoint();

        // When a new scene loads, the old weapons were destroyed. Respawn them!
        SpawnWeaponsForClient(OwnerClientId);
    }

    private void TeleportToSpawnPoint()
    {
        if (!IsOwner) return;

        var spawnPoints = FindObjectsOfType<PlayerSpawnPoint>();
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
