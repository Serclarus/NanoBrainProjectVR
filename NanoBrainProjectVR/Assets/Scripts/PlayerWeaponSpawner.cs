using Unity.Netcode;
using UnityEngine;

[Tooltip("Attach this to your Network Player Prefab so each player spawns with their own set of weapons!")]
public class PlayerWeaponSpawner : NetworkBehaviour
{
    [Header("Weapons to Spawn")]
    [Tooltip("The networked prefab of the Rifle")]
    public GameObject riflePrefab;
    
    [Tooltip("The networked prefab of the Shotgun")]
    public GameObject shotgunPrefab;
    
    [Tooltip("The networked prefab of the Pistol")]
    public GameObject pistolPrefab;

    public override void OnNetworkSpawn()
    {
        // Only the server/host has the authority to spawn Network Objects
        if (IsServer)
        {
            // Spawn the weapons and strictly give ownership to this specific player!
            SpawnWeapon(riflePrefab);
            SpawnWeapon(shotgunPrefab);
            SpawnWeapon(pistolPrefab);
        }
        else if (IsOwner)
        {
            // If we are a client joining the game, ask the server to spawn our weapons for us
            SpawnWeaponsServerRpc();
        }
    }

    [ServerRpc]
    private void SpawnWeaponsServerRpc()
    {
        SpawnWeapon(riflePrefab);
        SpawnWeapon(shotgunPrefab);
        SpawnWeapon(pistolPrefab);
    }

    private void SpawnWeapon(GameObject prefab)
    {
        if (prefab == null) return;

        // Spawn the weapon into the world
        GameObject spawnedWeapon = Instantiate(prefab, transform.position, transform.rotation);
        
        NetworkObject netObj = spawnedWeapon.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // Give ownership of this weapon exclusively to the player whose body spawned it!
            // This ensures their WeaponAutoReturn script will trigger and slot it into their personal holsters!
            netObj.SpawnWithOwnership(OwnerClientId);
        }
        else
        {
            Debug.LogError($"<color=red>[PlayerWeaponSpawner]</color> {prefab.name} does not have a NetworkObject attached!");
        }
    }
}
