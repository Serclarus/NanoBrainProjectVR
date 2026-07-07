using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(NetworkObject))]
[Tooltip("Attach this directly to your VR Rig in the Scene (e.g., XR Interaction Setup)")]
public class PlayerWeaponSpawner : NetworkBehaviour
{
    [Header("Networked Weapon Prefabs")]
    public GameObject riflePrefab;
    public GameObject shotgunPrefab;
    public GameObject pistolPrefab;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"<color=green>[PlayerWeaponSpawner]</color> OnNetworkSpawn fired! Server Status: {IsServer}");

        // Only the Server has the authority to spawn physical networked weapons for the players
        if (!IsServer) return;

        Debug.Log($"<color=green>[PlayerWeaponSpawner]</color> Automatically generating weapons for Player {OwnerClientId}...");

        PlayerHolsterSystem holsters = GetComponentInChildren<PlayerHolsterSystem>();
        
        if (holsters == null)
        {
            Debug.LogError("<color=red>[CRITICAL ERROR]</color> Could not find PlayerHolsterSystem anywhere inside this VR Rig! Weapons cannot be holstered!");
        }

        SpawnWeapon(riflePrefab, holsters != null ? holsters.rightShoulderSocket : null);
        SpawnWeapon(shotgunPrefab, holsters != null ? holsters.leftShoulderSocket : null);
        SpawnWeapon(pistolPrefab, holsters != null ? holsters.rightBeltSocket : null);
    }

    private void SpawnWeapon(GameObject prefab, XRSocketInteractor targetSocket)
    {
        if (prefab == null)
        {
            Debug.LogWarning("<color=yellow>[PlayerWeaponSpawner]</color> A weapon prefab is missing in the inspector! Skipping.");
            return;
        }

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        if (targetSocket != null)
        {
            spawnPos = targetSocket.transform.position;
            spawnRot = targetSocket.transform.rotation;
        }
        else
        {
            Debug.LogWarning($"<color=yellow>[PlayerWeaponSpawner]</color> Socket for {prefab.name} is missing or HolsterSystem wasn't found! Spawning at feet.");
        }

        GameObject spawnedWeapon = Instantiate(prefab, spawnPos, spawnRot);
        
        NetworkObject netObj = spawnedWeapon.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnWithOwnership(OwnerClientId);
            spawnedWeapon.name = $"{prefab.name}_Player_{OwnerClientId}";
            Debug.Log($"<color=cyan>[PlayerWeaponSpawner]</color> Successfully spawned and networked {spawnedWeapon.name}!");
        }
        else
        {
            Debug.LogError($"<color=red>[PlayerWeaponSpawner]</color> {prefab.name} is missing a NetworkObject component! It cannot be spawned on the network!");
            Destroy(spawnedWeapon);
        }
    }
}
