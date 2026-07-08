using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Spawns networked weapons when connected to the network.
/// Plain MonoBehaviour — no NetworkObject needed on the VR rig.
/// Works with both Client-Server AND Distributed Authority topologies.
/// </summary>
public class PlayerWeaponSpawner : MonoBehaviour
{
    [Header("Weapon Prefabs (must be registered in NetworkManager's prefab list)")]
    public GameObject riflePrefab;
    public GameObject shotgunPrefab;
    public GameObject pistolPrefab;

    private bool hasSpawned = false;

    private void Start()
    {
        Debug.Log("<color=green>[WeaponSpawner]</color> Script is ALIVE on: " + gameObject.name);
    }

    private void Update()
    {
        if (hasSpawned) return;
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.IsListening) return;
        if (!NetworkManager.Singleton.IsConnectedClient) return;

        hasSpawned = true;

        Debug.Log("<color=green>[WeaponSpawner]</color> Connected to network — spawning weapons now.");

        PlayerHolsterSystem holsters = GetComponentInChildren<PlayerHolsterSystem>();

        if (holsters == null)
        {
            Debug.LogWarning("<color=yellow>[WeaponSpawner]</color> No PlayerHolsterSystem found. Weapons will spawn at player position.");
        }

        SpawnWeapon(riflePrefab, holsters);
        SpawnWeapon(shotgunPrefab, holsters);
        SpawnWeapon(pistolPrefab, holsters);
    }

    private void SpawnWeapon(GameObject prefab, PlayerHolsterSystem holsters)
    {
        if (prefab == null)
        {
            Debug.LogWarning("<color=yellow>[WeaponSpawner]</color> A weapon slot is empty in the Inspector. Skipping.");
            return;
        }

        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;

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

        WeaponAutoReturn spawnedAutoReturn = weapon.GetComponent<WeaponAutoReturn>();
        XRSocketInteractor targetSocket = null;

        if (holsters != null && spawnedAutoReturn != null)
        {
            if (spawnedAutoReturn.slotType == WeaponSlotType.Rifle) targetSocket = holsters.rightShoulderSocket;
            else if (spawnedAutoReturn.slotType == WeaponSlotType.Shotgun) targetSocket = holsters.leftShoulderSocket;
            else if (spawnedAutoReturn.slotType == WeaponSlotType.Pistol) targetSocket = holsters.rightBeltSocket;
            
            // Inject the exact socket into the script!
            spawnedAutoReturn.homeSocket = targetSocket;
        }

        NetworkObject netObj = weapon.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"<color=red>[WeaponSpawner]</color> {prefab.name} has no NetworkObject! Cannot spawn on network.");
            Destroy(weapon);
            return;
        }

        // Tell Netcode for GameObjects NOT to destroy this object when loading a new scene!
        netObj.DestroyWithScene = false;

        netObj.Spawn();
        weapon.name = $"{prefab.name}_Networked";

        // Tell Unity NOT to destroy this object when loading a new scene!
        DontDestroyOnLoad(weapon);

        Debug.Log($"<color=cyan>[WeaponSpawner]</color> Spawned {weapon.name} and explicitly wired it to its socket!");
    }
}
