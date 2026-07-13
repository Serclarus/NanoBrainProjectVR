using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerLoadout : NetworkBehaviour
{
    [Header("Weapon Prefabs (MUST be in NetworkManager list!)")]
    public GameObject shotgunPrefab;
    public GameObject riflePrefab;
    public GameObject pistolPrefab;

    public override void OnNetworkSpawn()
    {
        // ONLY the server is allowed to spawn Network Objects.
        if (IsServer)
        {
            // If this player body belongs to the PC Operator (who has no VR headset), DO NOT spawn weapons for them!
            bool isVRActive = false;
            var xrDisplays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            UnityEngine.SubsystemManager.GetSubsystems(xrDisplays);
            foreach (var display in xrDisplays) if (display.running) isVRActive = true;

            if (OwnerClientId == NetworkManager.Singleton.LocalClientId && !isVRActive)
            {
                Debug.Log("[NetworkPlayerLoadout] PC Operator detected! Skipping weapon spawn.");
                return;
            }

            // Spawn weapons immediately when the VR player joins
            SpawnWeaponsForClient(OwnerClientId);
            
            // Subscribe to scene load events so we can respawn weapons when the map changes!
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        // When the server loads a new map, the old weapons were destroyed. Respawn them!
        SpawnWeaponsForClient(OwnerClientId);
    }

    private void SpawnWeaponsForClient(ulong clientId)
    {
        // The server physically creates the weapons and hands ownership to the player
        if (shotgunPrefab != null) SpawnAndAssign(shotgunPrefab, clientId);
        if (riflePrefab != null) SpawnAndAssign(riflePrefab, clientId);
        if (pistolPrefab != null) SpawnAndAssign(pistolPrefab, clientId);
    }

    private void SpawnAndAssign(GameObject prefab, ulong clientId)
    {
        // Spawn slightly above the player to prevent clipping into the floor
        Vector3 spawnPos = transform.position + (Vector3.up * 1.5f);
        GameObject wep = Instantiate(prefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = wep.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(clientId);
        }
    }
}
