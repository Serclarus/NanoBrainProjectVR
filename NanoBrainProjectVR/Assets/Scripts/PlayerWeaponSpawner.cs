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
        Debug.Log($"<color=green>[PlayerWeaponSpawner]</color> OnNetworkSpawn fired for {gameObject.name}! IsOwner: {IsOwner}, IsServer: {IsServer}");

        // The SERVER dictates the weapons for EVERY player!
        // When ANY player's body spawns on the network, the Server automatically creates weapons for that body!
        if (IsServer)
        {
            Debug.Log($"<color=green>[PlayerWeaponSpawner]</color> SERVER: Automatically generating networked weapons for Player {OwnerClientId}...");
            SpawnWeapon(riflePrefab, false, OwnerClientId);
            SpawnWeapon(shotgunPrefab, false, OwnerClientId);
            SpawnWeapon(pistolPrefab, false, OwnerClientId);
        }
    }

    private GameObject SpawnWeapon(GameObject prefab, bool isOffline, ulong specificOwnerId = 0)
    {
        if (prefab == null)
        {
            Debug.LogError($"<color=red>[PlayerWeaponSpawner]</color> FAILED TO SPAWN! The weapon prefab slot is EMPTY in the Inspector! Did you forget to apply your Prefab Overrides?");
            return null;
        }

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        PlayerHolsterSystem holsters = GetComponentInChildren<PlayerHolsterSystem>(true);
        if (holsters != null)
        {
            WeaponAutoReturn autoReturn = prefab.GetComponent<WeaponAutoReturn>();
            if (autoReturn != null)
            {
                if (autoReturn.slotType == WeaponSlotType.Rifle && holsters.rightShoulderSocket != null)
                {
                    spawnPos = holsters.rightShoulderSocket.transform.position;
                    spawnRot = holsters.rightShoulderSocket.transform.rotation;
                }
                else if (autoReturn.slotType == WeaponSlotType.Shotgun && holsters.leftShoulderSocket != null)
                {
                    spawnPos = holsters.leftShoulderSocket.transform.position;
                    spawnRot = holsters.leftShoulderSocket.transform.rotation;
                }
                else if (autoReturn.slotType == WeaponSlotType.Pistol && holsters.rightBeltSocket != null)
                {
                    spawnPos = holsters.rightBeltSocket.transform.position;
                    spawnRot = holsters.rightBeltSocket.transform.rotation;
                }
            }
        }

        GameObject spawnedWeapon = Instantiate(prefab, spawnPos, spawnRot);
        
        if (!isOffline)
        {
            NetworkObject netObj = spawnedWeapon.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                ulong finalOwnerId = IsServer && specificOwnerId == 0 ? OwnerClientId : specificOwnerId;
                netObj.SpawnWithOwnership(finalOwnerId);
                
                spawnedWeapon.name = $"{prefab.name}_Player_{finalOwnerId}";
                Debug.Log($"<color=cyan>[PlayerWeaponSpawner]</color> Spawned {spawnedWeapon.name} and gave ownership to Client ID {finalOwnerId}");
            }
            else
            {
                Debug.LogError($"<color=red>[PlayerWeaponSpawner]</color> {prefab.name} does not have a NetworkObject attached!");
            }
        }
        
        return spawnedWeapon;
    }
}
