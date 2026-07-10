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
        
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("<color=orange>[WeaponSpawner]</color> Waiting: NetworkManager.Singleton is NULL!");
            return;
        }
        
        if (!NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("<color=orange>[WeaponSpawner]</color> Waiting: Network is not listening yet!");
            return;
        }
        
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            return;
        }

        NetworkObject myNetObj = GetComponent<NetworkObject>();
        if (myNetObj == null || !myNetObj.IsSpawned || !myNetObj.IsOwner)
        {
            if (myNetObj == null || myNetObj.IsSpawned)
            {
                if (!hasSpawned) Debug.Log($"<color=red>[WeaponSpawner]</color> Ignoring spawn on {gameObject.name} because it either has no NetworkObject, or we do not own it. (myNetObj is {myNetObj?.name})");
                hasSpawned = true; 
            }
            return;
        }

        hasSpawned = true;
        
        // CRITICAL FIX: Make the avatar persist across scenes so it isn't destroyed when loading maps!
        // If it gets destroyed, NetworkManager spawns a new one, which creates duplicate weapons!
        DontDestroyOnLoad(gameObject);

        Debug.Log($"<color=green>[WeaponSpawner]</color> Connected to network and we own this Avatar ({gameObject.name})! Spawning 3 weapons NOW.");

        PlayerHolsterSystem holsters = GetComponentInChildren<PlayerHolsterSystem>();

        if (holsters == null)
        {
            Debug.LogWarning("<color=yellow>[WeaponSpawner]</color> No PlayerHolsterSystem found locally on Avatar. Weapons will spawn at player position and rely on AutoReturn fallback.");
        }

        SpawnWeapon(riflePrefab, holsters);
        SpawnWeapon(shotgunPrefab, holsters);
        SpawnWeapon(pistolPrefab, holsters);
    }

    private void SpawnWeapon(GameObject prefab, PlayerHolsterSystem holsters)
    {
        if (prefab == null) return;
        
        Debug.Log($"<color=cyan>[WeaponSpawner]</color> Spawning {prefab.name}...");

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

        netObj.Spawn();
        weapon.name = $"{prefab.name}_Networked";

        Debug.Log($"<color=cyan>[WeaponSpawner]</color> Spawned {weapon.name} and explicitly wired it to its socket!");
    }
}
