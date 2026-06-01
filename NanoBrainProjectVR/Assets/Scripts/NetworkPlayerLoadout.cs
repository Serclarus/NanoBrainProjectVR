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
        // Only the player who owns this body gets to spawn their weapons!
        if (IsOwner)
        {
            SpawnWeaponsServerRpc(OwnerClientId);
        }
    }

    [ServerRpc]
    private void SpawnWeaponsServerRpc(ulong clientId)
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
