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
            // Spawn weapons immediately when the player joins
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
        GameObject wep = Instantiate(prefab);
        NetworkObject netObj = wep.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(clientId);
        }
    }
}
