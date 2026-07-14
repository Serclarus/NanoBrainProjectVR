using Unity.Netcode;
using UnityEngine;

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

    public override void OnNetworkSpawn()
    {
        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> OnNetworkSpawn fired!" +
                  $" GameObject: {gameObject.name}" +
                  $" IsOwner: {IsOwner}" +
                  $" IsServer: {IsServer}" +
                  $" OwnerClientId: {OwnerClientId}" +
                  $" LocalClientId: {NetworkManager.Singleton.LocalClientId}" +
                  $" DeviceType: {SystemInfo.deviceType}" +
                  $" XRActive: {UnityEngine.XR.XRSettings.isDeviceActive}");

        if (IsOwner)
        {
            // Determine if the local owner is a VR user or PC Operator
            bool isVR = true;
            if (SystemInfo.deviceType == DeviceType.Desktop && !UnityEngine.XR.XRSettings.isDeviceActive)
            {
                isVR = false;
            }

            // Sync the state to everyone
            isVRUser.Value = isVR;

            if (!isVR)
            {
                HideAvatarBody();
            }
            else
            {
                debugStatus = $"Spawning weapons for Client {OwnerClientId}!";
                Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> {debugStatus}");
                SpawnWeaponsForClient(OwnerClientId);

                // Subscribe to scene load events so we can respawn weapons when the map changes!
                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
                    Debug.Log($"<color=green>[NetworkPlayerLoadout]</color> Subscribed to OnLoadEventCompleted for scene changes.");
                }
            }
        }
        else
        {
            // Non-owners listen to state changes to hide the avatar if it's a PC Operator
            isVRUser.OnValueChanged += OnVRUserChanged;

            // Run initial check for already spawned clients (late joiners)
            if (!isVRUser.Value)
            {
                HideAvatarBody();
            }
        }
    }

    private void OnVRUserChanged(bool previousValue, bool newValue)
    {
        if (!newValue)
        {
            HideAvatarBody();
        }
    }

    private void HideAvatarBody()
    {
        debugStatus = $"Hidden PC Operator (Client {OwnerClientId})";
        Debug.Log($"<color=yellow>[NetworkPlayerLoadout]</color> {debugStatus} globally - disabling cameras, renderers, and colliders.");

        // Disable all cameras, renderers, and colliders on the PC Operator's avatar body
        foreach (var cam in GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;

        // Also disable the XR Origin component entirely so it doesn't try to track/interact
        var xrOrigin = GetComponentInChildren<Unity.XR.CoreUtils.XROrigin>(true);
        if (xrOrigin != null) xrOrigin.gameObject.SetActive(false);
    }

    private void OnGUI()
    {
        // Show diagnostics on the owner's screen (VR or PC)
        if (!IsOwner) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.cyan;

        GUILayout.BeginArea(new Rect(10, 10, 800, 800));
        GUILayout.Label($"--- NETWORK PLAYER LOADOUT ---", style);
        GUILayout.Label($"My ClientId: {OwnerClientId}", style);
        GUILayout.Label($"IsOwner: {IsOwner} | IsServer: {IsServer}", style);
        GUILayout.Label($"Status: {debugStatus}", style);
        
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
        Debug.Log($"<color=cyan>[NetworkPlayerLoadout]</color> Scene loaded: {sceneName}. Respawning weapons.");
        // When a new scene loads, the old weapons were destroyed. Respawn them!
        SpawnWeaponsForClient(OwnerClientId);
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
