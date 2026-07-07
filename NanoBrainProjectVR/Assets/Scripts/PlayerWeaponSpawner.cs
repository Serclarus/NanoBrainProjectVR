using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawns networked weapons when the host connects.
/// This is a plain MonoBehaviour — it does NOT need a NetworkObject on the VR rig.
/// Attach it anywhere on your VR rig hierarchy.
/// </summary>
public class PlayerWeaponSpawner : MonoBehaviour
{
    [Header("Weapon Prefabs (must be registered in NetworkManager's prefab list)")]
    public GameObject riflePrefab;
    public GameObject shotgunPrefab;
    public GameObject pistolPrefab;

    private bool hasSpawned = false;

    private void OnEnable()
    {
        // Wait for NetworkManager to exist, then subscribe
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
    }

    /// <summary>
    /// Called the instant the local machine successfully starts as Host/Server.
    /// </summary>
    private void OnServerStarted()
    {
        if (hasSpawned) return;
        hasSpawned = true;

        Debug.Log("<color=green>[WeaponSpawner]</color> Server started — spawning weapons now.");

        SpawnWeapon(riflePrefab);
        SpawnWeapon(shotgunPrefab);
        SpawnWeapon(pistolPrefab);
    }

    private void SpawnWeapon(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("<color=yellow>[WeaponSpawner]</color> A weapon slot is empty in the Inspector. Skipping.");
            return;
        }

        // Find the matching holster socket so the weapon appears in the right place
        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

        PlayerHolsterSystem holsters = GetComponentInChildren<PlayerHolsterSystem>();
        WeaponAutoReturn autoReturn = prefab.GetComponent<WeaponAutoReturn>();

        if (holsters != null && autoReturn != null)
        {
            if (autoReturn.slotType == WeaponSlotType.Rifle && holsters.rightShoulderSocket != null)
            {
                pos = holsters.rightShoulderSocket.transform.position;
                rot = holsters.rightShoulderSocket.transform.rotation;
            }
            else if (autoReturn.slotType == WeaponSlotType.Shotgun && holsters.leftShoulderSocket != null)
            {
                pos = holsters.leftShoulderSocket.transform.position;
                rot = holsters.leftShoulderSocket.transform.rotation;
            }
            else if (autoReturn.slotType == WeaponSlotType.Pistol && holsters.rightBeltSocket != null)
            {
                pos = holsters.rightBeltSocket.transform.position;
                rot = holsters.rightBeltSocket.transform.rotation;
            }
        }

        GameObject weapon = Instantiate(prefab, pos, rot);

        NetworkObject netObj = weapon.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"<color=red>[WeaponSpawner]</color> {prefab.name} has no NetworkObject! Cannot network-spawn it.");
            Destroy(weapon);
            return;
        }

        netObj.Spawn();
        weapon.name = $"{prefab.name}_Networked";
        Debug.Log($"<color=cyan>[WeaponSpawner]</color> Spawned {weapon.name} at {pos}");
    }
}
