using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class AmmoPouch : Unity.Netcode.NetworkBehaviour, IXRSelectFilter
{
    [Header("Ammo Pouch Settings")]
    [Tooltip("The socket that will hold the ammo")]
    public XRSocketInteractor socketInteractor;
    
    [Tooltip("The current magazine prefab this pouch produces")]
    public GameObject magazinePrefab;

    [System.Serializable]
    public struct AmmoPoolConfig
    {
        public GameObject ammoPrefab;
        public int initialPoolSize;
    }

    [Header("Pre-warmed Pools")]
    [Tooltip("Configure which magazines to pre-instantiate and how many to keep in the circular pool. (e.g. 4 pistol, 4 rifle, 7 shotgun shells)")]
    public List<AmmoPoolConfig> prewarmedPools = new List<AmmoPoolConfig>();

    private Dictionary<GameObject, Queue<GameObject>> ammoPools = new Dictionary<GameObject, Queue<GameObject>>();

    private bool isRefilling = false;
    
    // We use this variable to bypass our own grab filter when RefillSocketRoutine executes!
    private bool allowProgrammaticGrab = false;

    private GameObject hiddenAmmoInstance;
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private Material invisibleMaterial;

    public bool canProcess => true;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        // If the Refill routine is forcing a grab, allow it!
        if (allowProgrammaticGrab) return true;

        // CRITICAL FIX: XR Interaction Toolkit continuously evaluates this filter EVERY FRAME.
        // If we return false after the item is already selected, XRI will forcefully drop it!
        // We must return true if the socket already holds this specific interactable!
        if (socketInteractor != null && socketInteractor.hasSelection && socketInteractor.interactablesSelected[0] == interactable)
        {
            return true;
        }

        // Otherwise, reject organics grabs (e.g. dropped shotguns flying into the pouch)
        return false;
    }

    private void Awake()
    {
        socketInteractor = GetComponent<XRSocketInteractor>();
        
        // Register this script as the Select Filter
        socketInteractor.selectFilters.Add(this);
        socketInteractor.selectExited.AddListener(OnItemRemovedFromSocket);

        // Fix: Ensure the Ammo Pouch doesn't spawn ghost meshes!
        if (socketInteractor.interactableCantHoverMeshMaterial != null)
        {
            socketInteractor.interactableCantHoverMeshMaterial = null;
        }

        // Create a guaranteed invisible material using a universally compatible shader
        // UI/Default exists in URP, HDRP, and Built-in, preventing the "Pink Material" error!
        invisibleMaterial = new Material(Shader.Find("UI/Default"));
        invisibleMaterial.color = new Color(0, 0, 0, 0); // 100% transparent
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // CRITICAL DUAL-TOPOLOGY FIXED:
        // We initialize pools ONLY if we are the Server! 
        // Clients will automatically receive the spawned objects from the Server via standard Netcode synchronization.
        if (IsServer)
        {
            Debug.Log($"<color=cyan>[AmmoPouch]</color> OnNetworkSpawn. IsServer: {IsServer}. Initializing pools.");
            InitializePools();
        }
    }

    private void OnDestroy()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectExited.RemoveListener(OnItemRemovedFromSocket);
            socketInteractor.selectFilters.Remove(this);
        }
    }

    private void InitializePools()
    {
        Transform poolParent = GameObject.Find("AmmoPouch_Pool")?.transform;
        if (poolParent == null)
        {
            poolParent = new GameObject("AmmoPouch_Pool").transform;
            DontDestroyOnLoad(poolParent.gameObject);
        }

        foreach (var config in prewarmedPools)
        {
            if (config.ammoPrefab == null) continue;

            if (!ammoPools.ContainsKey(config.ammoPrefab))
            {
                ammoPools[config.ammoPrefab] = new Queue<GameObject>();
            }

            Debug.Log($"<color=cyan>[AmmoPouch]</color> Pre-warming pool for {config.ammoPrefab.name} with size {config.initialPoolSize}");

            List<ulong> spawnedIds = new List<ulong>();

            for (int i = 0; i < config.initialPoolSize; i++)
            {
                GameObject newAmmo = Instantiate(config.ammoPrefab, poolParent);
                newAmmo.name = config.ammoPrefab.name + "_Pooled_" + i;
                
                Unity.Netcode.NetworkObject netObj = newAmmo.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null)
                {
                    try
                    {
                        // CRITICAL NETCODE FIX: The object MUST be active when SpawnWithOwnership is called!
                        // If it is inactive, Unity Netcode writes ZERO bytes for its NetworkBehaviours,
                        // which catastrophically breaks the `SynchronizeSceneNetworkObjects` stream for Clients
                        // and results in 'NetworkBehaviour index out of bounds' errors!
                        netObj.SpawnWithOwnership(OwnerClientId);
                        
                        // Now that it's legally registered with Netcode, we can hide it.
                        newAmmo.SetActive(false);
                        
                        spawnedIds.Add(netObj.NetworkObjectId);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"<color=yellow>[AmmoPouch]</color> Client failed to spawn magazine in pool (expected in Client-Server mode). Destroying local clone. Error: {e.Message}");
                        Destroy(newAmmo);
                        continue;
                    }
                }
                
                ammoPools[config.ammoPrefab].Enqueue(newAmmo);
            }

            // In Client-Server mode, the Server spawned these magazines. 
            // It MUST notify the Client about their NetworkObjectIds so the Client can add them to its local pool!
            if (spawnedIds.Count > 0 && IsServer && !IsOwner)
            {
                RegisterPooledAmmoClientRpc(config.ammoPrefab.name, spawnedIds.ToArray());
            }
        }
    }

    [Unity.Netcode.ClientRpc]
    private void RegisterPooledAmmoClientRpc(string prefabName, ulong[] netIds)
    {
        if (IsServer) return; // Server already enqueued them directly during instantiation!

        GameObject matchingPrefab = null;
        foreach (var config in prewarmedPools)
        {
            if (config.ammoPrefab != null && config.ammoPrefab.name == prefabName)
            {
                matchingPrefab = config.ammoPrefab;
                break;
            }
        }

        if (matchingPrefab == null) return;

        if (!ammoPools.ContainsKey(matchingPrefab))
            ammoPools[matchingPrefab] = new Queue<GameObject>();

        StartCoroutine(WaitForSpawnedObjects(matchingPrefab, netIds));
    }

    private IEnumerator WaitForSpawnedObjects(GameObject matchingPrefab, ulong[] netIds)
    {
        Transform poolParent = GameObject.Find("AmmoPouch_Pool")?.transform;
        if (poolParent == null)
        {
            poolParent = new GameObject("AmmoPouch_Pool").transform;
            DontDestroyOnLoad(poolParent.gameObject);
        }

        foreach (ulong id in netIds)
        {
            float timeout = 5f;
            Unity.Netcode.NetworkObject netObj = null;
            
            while (timeout > 0f)
            {
                if (Unity.Netcode.NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out netObj))
                {
                    break;
                }
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (netObj != null)
            {
                ammoPools[matchingPrefab].Enqueue(netObj.gameObject);
                netObj.gameObject.SetActive(false);
                netObj.transform.SetParent(poolParent);
                Debug.Log($"<color=green>[AmmoPouch]</color> Client successfully synced and enqueued server-spawned magazine: {netObj.gameObject.name}");
            }
        }
    }

    private GameObject GetAmmoFromPool(GameObject prefab)
    {
        if (prefab == null) return null;

        if (!ammoPools.ContainsKey(prefab))
        {
            ammoPools[prefab] = new Queue<GameObject>();
        }

        GameObject ammoInstance = null;

        // Try to find an inactive or unheld one
        int queueSize = ammoPools[prefab].Count;
        for (int i = 0; i < queueSize; i++)
        {
            GameObject candidate = ammoPools[prefab].Dequeue();
            ammoPools[prefab].Enqueue(candidate); // Re-queue it immediately for circular pooling
            
            // A magazine is "free" to steal if it is NOT selected (not held by hand, not in a gun socket)
            XRGrabInteractable grab = candidate.GetComponent<XRGrabInteractable>();
            if (grab != null && !grab.isSelected)
            {
                ammoInstance = candidate;
                break; // Found one!
            }
        }

        // If all pooled magazines are currently in use, expand the pool dynamically
        if (ammoInstance == null)
        {
            Debug.LogWarning($"<color=yellow>[AmmoPouch]</color> All pooled {prefab.name} are currently in use! Expanding pool dynamically.");
            
            Transform poolParent = GameObject.Find("AmmoPouch_Pool")?.transform;
            ammoInstance = Instantiate(prefab, transform.position, transform.rotation, poolParent);
            Unity.Netcode.NetworkObject netObj = ammoInstance.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null)
            {
                try
                {
                    netObj.SpawnWithOwnership(OwnerClientId);
                    if (IsServer && !IsOwner)
                    {
                        RegisterPooledAmmoClientRpc(prefab.name, new ulong[] { netObj.NetworkObjectId });
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"<color=yellow>[AmmoPouch]</color> Client failed to spawn dynamic pool expansion (expected in CS mode). Destroying local clone. Error: {e.Message}");
                    Destroy(ammoInstance);
                    return null;
                }
            }
            ammoPools[prefab].Enqueue(ammoInstance);
        }

        // Teleport out of the void pool so its colliders are physically inside the Ammo Pouch
        Transform attach = socketInteractor.attachTransform != null ? socketInteractor.attachTransform : transform;
        ammoInstance.transform.position = attach.position;
        ammoInstance.transform.rotation = attach.rotation;

        Rigidbody rb = ammoInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // FORCE kinematic so it doesn't explode in physics calculations
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Magazine mag = ammoInstance.GetComponent<Magazine>();
        if (mag != null)
        {
            mag.Refill();
        }
        else
        {
            // For items like Shotgun Shells that don't use Magazine.cs, attach our generic physics fixer
            if (ammoInstance.GetComponent<DropPhysicsFixer>() == null)
            {
                ammoInstance.AddComponent<DropPhysicsFixer>();
            }
        }

        ammoInstance.SetActive(true);
        return ammoInstance;
    }

    public void SetMagazinePrefab(GameObject newMagazinePrefab)
    {
        if (newMagazinePrefab != null && newMagazinePrefab != magazinePrefab)
        {
            magazinePrefab = newMagazinePrefab;
            RefillSocket();
        }
        else if (newMagazinePrefab != null && !socketInteractor.hasSelection)
        {
            RefillSocket();
        }
    }

    private void OnItemRemovedFromSocket(SelectExitEventArgs args)
    {
        GameObject grabbedAmmo = args.interactableObject.transform.gameObject;
        
        // Stop tracking it and restore its original visible materials!
        if (hiddenAmmoInstance != null && grabbedAmmo == hiddenAmmoInstance)
        {
            RestoreMaterials(hiddenAmmoInstance);
            hiddenAmmoInstance = null;
        }

        RefillSocket();
    }

    private void RefillSocket()
    {
        if (!IsOwner) return; // Only the owner should perform local refills of their pouch
        StartCoroutine(RefillSocketRoutine());
    }

    private IEnumerator RefillSocketRoutine()
    {
        if (isRefilling) yield break;
        isRefilling = true;

        yield return new WaitForSeconds(0.1f);

        if (magazinePrefab == null)
        {
            isRefilling = false;
            yield break;
        }

        if (socketInteractor.hasSelection)
        {
            IXRSelectInteractable oldItem = socketInteractor.interactablesSelected[0];
            socketInteractor.interactionManager.SelectCancel((IXRSelectInteractor)socketInteractor, oldItem);
            
            // Cleanly despawn the old magazine off the network so it doesn't float in mid-air
            Magazine oldMag = oldItem.transform.GetComponent<Magazine>();
            if (oldMag != null)
            {
                oldMag.InstantDespawn();
            }
            else
            {
                oldItem.transform.gameObject.SetActive(false);
            }
            
            yield return new WaitForEndOfFrame(); 
        }

        GameObject newAmmo = null;
        while (newAmmo == null)
        {
            newAmmo = GetAmmoFromPool(magazinePrefab);
            if (newAmmo == null) yield return new WaitForSeconds(0.5f); // Wait for a magazine to become free
        }
        
        // Track this specific instance so we can restore its materials later!
        hiddenAmmoInstance = newAmmo;
        
        ApplyInvisibleMaterial(newAmmo);

        XRBaseInteractable ammoInteractable = newAmmo.GetComponentInChildren<XRBaseInteractable>(true);

        yield return new WaitForEndOfFrame();

        if (ammoInteractable != null && socketInteractor.interactionManager != null)
        {
            // Temporarily bypass our own filter to forcefully inject the ammo!
            allowProgrammaticGrab = true;
            socketInteractor.interactionManager.SelectEnter((IXRSelectInteractor)socketInteractor, (IXRSelectInteractable)ammoInteractable);
            allowProgrammaticGrab = false;
        }

        isRefilling = false;
    }

    private void ApplyInvisibleMaterial(GameObject ammoInstance)
    {
        originalMaterials.Clear();
        
        Unity.Netcode.NetworkObject netObj = ammoInstance.GetComponentInParent<Unity.Netcode.NetworkObject>();
        Transform trueRoot = netObj != null ? netObj.transform : ammoInstance.transform;

        Renderer[] renderers = trueRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            originalMaterials[r] = r.sharedMaterials;

            Material[] invMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < invMats.Length; i++)
            {
                invMats[i] = invisibleMaterial;
            }
            r.sharedMaterials = invMats;
        }
    }

    private void RestoreMaterials(GameObject ammoInstance)
    {
        if (ammoInstance == null) return;
        
        Unity.Netcode.NetworkObject netObj = ammoInstance.GetComponentInParent<Unity.Netcode.NetworkObject>();
        Transform trueRoot = netObj != null ? netObj.transform : ammoInstance.transform;

        Renderer[] renderers = trueRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (originalMaterials.ContainsKey(r))
            {
                r.sharedMaterials = originalMaterials[r];
            }
        }
        
        originalMaterials.Clear();
    }
}
