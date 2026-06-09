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

    private GameObject offlineRifle;
    private GameObject offlineShotgun;
    private GameObject offlinePistol;

    private void Start()
    {
        // If the game starts without the network running, spawn offline weapons immediately!
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            offlineRifle = SpawnWeapon(riflePrefab, true);
            offlineShotgun = SpawnWeapon(shotgunPrefab, true);
            offlinePistol = SpawnWeapon(pistolPrefab, true);
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"<color=green>[PlayerWeaponSpawner]</color> OnNetworkSpawn fired for {gameObject.name}! IsOwner: {IsOwner}, IsServer: {IsServer}");

        // The network just connected! 
        // We MUST destroy the temporary offline weapons. If NGO auto-spawned them, we must use Despawn!
        DestroyOfflineWeapon(offlineRifle);
        DestroyOfflineWeapon(offlineShotgun);
        DestroyOfflineWeapon(offlinePistol);

        // NGO BUG FIX: Clients cannot reliably send RPCs during their initial connection sweep!
        // Instead of the Client asking for weapons, the SERVER must automatically issue weapons
        // to EVERY player the instant their player rig officially spawns on the server!
        if (IsServer)
        {
            Debug.Log($"<color=green>[PlayerWeaponSpawner]</color> SERVER: Automatically generating networked weapons for Player {OwnerClientId}...");
            SpawnWeapon(riflePrefab, false, OwnerClientId);
            SpawnWeapon(shotgunPrefab, false, OwnerClientId);
            SpawnWeapon(pistolPrefab, false, OwnerClientId);
        }
    }

    private void DestroyOfflineWeapon(GameObject offlineWeapon)
    {
        if (offlineWeapon != null)
        {
            NetworkObject no = offlineWeapon.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned)
            {
                if (IsServer)
                {
                    no.Despawn();
                }
                else
                {
                    // Clients cannot despawn network objects! We just hide/disable them locally so they don't interfere.
                    offlineWeapon.SetActive(false);
                }
            }
            else
            {
                Destroy(offlineWeapon);
            }
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
