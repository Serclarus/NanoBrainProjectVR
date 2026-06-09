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

    private void Start()
    {
        // If we are playing offline without the network running, spawn locally immediately!
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.Log($"<color=cyan>[PlayerWeaponSpawner]</color> Offline Mode! Spawning local weapons...");
            SpawnWeapon(riflePrefab, true);
            SpawnWeapon(shotgunPrefab, true);
            SpawnWeapon(pistolPrefab, true);
        }
    }

    public override void OnNetworkSpawn()
    {
        // We only want the OWNER of this specific player body to request weapons!
        if (IsOwner)
        {
            if (IsServer)
            {
                // We are the Host, spawning our own weapons directly.
                SpawnWeapon(riflePrefab, false);
                SpawnWeapon(shotgunPrefab, false);
                SpawnWeapon(pistolPrefab, false);
            }
            else
            {
                // We are a Client, asking the server to spawn our weapons for us.
                SpawnWeaponsServerRpc();
            }
        }
    }

    [ServerRpc]
    private void SpawnWeaponsServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong callerId = rpcParams.Receive.SenderClientId;
        SpawnWeapon(riflePrefab, false, callerId);
        SpawnWeapon(shotgunPrefab, false, callerId);
        SpawnWeapon(pistolPrefab, false, callerId);
    }

    private void SpawnWeapon(GameObject prefab, bool isOffline, ulong specificOwnerId = 0)
    {
        if (prefab == null) return;

        // MULTIPLAYER FIX:
        // By default, Instantiate spawns at the player's root. If the NetworkTransform is Server-Authoritative, 
        // the Client's WeaponAutoReturn script cannot teleport the weapon to the shoulder because the Server snaps it back!
        // We MUST spawn the weapon at the exact socket position on the Server first!
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        PlayerHolsterSystem holsters = GetComponentInChildren<PlayerHolsterSystem>(true);
        if (holsters != null)
        {
            WeaponAutoReturn autoReturn = prefab.GetComponent<WeaponAutoReturn>();
            if (autoReturn != null)
            {
                Debug.Log($"<color=magenta>[PlayerWeaponSpawner]</color> Preparing to spawn {prefab.name}. Detected SlotType: {autoReturn.slotType}");
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
            else
            {
                Debug.LogWarning($"<color=magenta>[PlayerWeaponSpawner]</color> Prefab {prefab.name} is missing the WeaponAutoReturn script!");
            }
        }
        else
        {
            Debug.LogWarning($"<color=magenta>[PlayerWeaponSpawner]</color> Could not find PlayerHolsterSystem on this player rig! Spawning at root.");
        }

        Debug.Log($"<color=cyan>[PlayerWeaponSpawner]</color> Instantiating {prefab.name} at World Position: {spawnPos}");
        // Spawn the weapon into the world exactly at the socket position!
        GameObject spawnedWeapon = Instantiate(prefab, spawnPos, spawnRot);
        
        if (!isOffline)
        {
            NetworkObject netObj = spawnedWeapon.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // Give ownership of this weapon strictly to the client who called the RPC!
                ulong finalOwnerId = IsServer && specificOwnerId == 0 ? OwnerClientId : specificOwnerId;
                netObj.SpawnWithOwnership(finalOwnerId);
                Debug.Log($"<color=cyan>[PlayerWeaponSpawner]</color> Spawned {prefab.name} and gave ownership to Client ID {finalOwnerId}");
            }
            else
            {
                Debug.LogError($"<color=red>[PlayerWeaponSpawner]</color> {prefab.name} does not have a NetworkObject attached!");
            }
        }
    }
}
