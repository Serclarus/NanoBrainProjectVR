using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

[System.Serializable]
public struct AmmoPoolConfig
{
    public GameObject ammoPrefab;
    [Tooltip("How many to pre-instantiate at the start of the game.")]
    public int initialPoolSize;
}

[RequireComponent(typeof(XRSocketInteractor))]
public class AmmoPouch : MonoBehaviour, IXRSelectFilter
{
    [Tooltip("The Magazine prefab that this pouch will currently dispense. This is updated dynamically by the WeaponController!")]
    public GameObject magazinePrefab;
    
    [Header("Circular Object Pool")]
    [Tooltip("Configure which magazines to pre-instantiate and how many to keep in the circular pool. (e.g. 4 pistol, 4 rifle, 7 shotgun shells)")]
    public AmmoPoolConfig[] prewarmedPools;

    private Dictionary<GameObject, Queue<GameObject>> ammoPools = new Dictionary<GameObject, Queue<GameObject>>();

    private XRSocketInteractor socketInteractor;
    private bool isRefilling = false;

    // We use this to only allow the code-driven SelectEnter to succeed!
    private bool allowProgrammaticGrab = false;

    public bool canProcess => true;

    private void Awake()
    {
        socketInteractor = GetComponent<XRSocketInteractor>();
        if (socketInteractor != null)
        {
            socketInteractor.selectExited.AddListener(OnItemRemovedFromSocket);
            // Add ourselves as a filter so we can reject native trigger grabs (like the Shotgun loading port!)
            socketInteractor.selectFilters.Add(this);
        }

        InitializePools();
    }

    private void OnDestroy()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectExited.RemoveListener(OnItemRemovedFromSocket);
            socketInteractor.selectFilters.Remove(this);
        }
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        // Only allow a grab if our Refill routine is forcing it!
        // This prevents the Ammo Pouch from organically sucking up dropped shotguns!
        return allowProgrammaticGrab;
    }

    private void Update()
    {
        // Brute-force visibility lock: If XRI or Netcode tries to turn the renderer back on, crush it instantly!
        if (socketInteractor != null && socketInteractor.hasSelection)
        {
            IXRSelectInteractable heldItem = socketInteractor.interactablesSelected[0];
            if (heldItem != null && heldItem.transform != null)
            {
                Renderer[] renderers = heldItem.transform.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    if (r.enabled) r.enabled = false;
                }
            }
        }
    }

    private void InitializePools()
    {
        // Create a hidden parent object to keep the hierarchy extremely clean
        // We leave this at the ROOT of the scene, otherwise dropped magazines will move with the player!
        Transform poolParent = new GameObject("AmmoPouch_Pool").transform;
        
        // CRITICAL FIX: Make the Ammo Pool survive scene transitions!
        // Without this, the Lobby's pool gets destroyed when loading Boar Hunting,
        // forcing the AmmoPouch to desperately instantiate broken, offset fallback magazines!
        DontDestroyOnLoad(poolParent.gameObject);

        foreach (var config in prewarmedPools)
        {
            if (config.ammoPrefab == null) continue;

            if (!ammoPools.ContainsKey(config.ammoPrefab))
            {
                ammoPools[config.ammoPrefab] = new Queue<GameObject>();
            }

            for (int i = 0; i < config.initialPoolSize; i++)
            {
                GameObject newAmmo = Instantiate(config.ammoPrefab, poolParent);
                newAmmo.SetActive(false); // Start hidden!
                
                ammoPools[config.ammoPrefab].Enqueue(newAmmo);
            }
        }
        Debug.Log($"<color=green>[AmmoPouch]</color> Pre-warmed pools initialized!");
    }

    private GameObject GetAmmoFromPool(GameObject prefab)
    {
        if (prefab == null) return null;

        // Ensure the pool exists (just in case they forgot to add it to the inspector list)
        if (!ammoPools.ContainsKey(prefab))
        {
            ammoPools[prefab] = new Queue<GameObject>();
        }

        GameObject ammoInstance = null;

        // Try to find a free instance in the circular queue
        int checkCount = ammoPools[prefab].Count;
        for (int i = 0; i < checkCount; i++)
        {
            GameObject candidate = ammoPools[prefab].Dequeue();

            if (candidate == null) continue; // Optimize: Do NOT put destroyed/null objects back in the queue!
            
            ammoPools[prefab].Enqueue(candidate); // Keep the circle going

            XRGrabInteractable grab = candidate.GetComponent<XRGrabInteractable>();
            
            // A magazine is "free" to steal if it is NOT selected (not held by hand, not in a gun socket)
            if (grab != null && !grab.isSelected)
            {
                ammoInstance = candidate;
                break; // Found one!
            }
        }

        // If we checked the entire pool and they are ALL currently inside guns or hands,
        // we MUST instantiate a new one to prevent breaking the game!
        if (ammoInstance == null)
        {
            Debug.LogWarning($"<color=yellow>[AmmoPouch]</color> All pooled {prefab.name} are currently inside guns! Expanding pool slightly.");
            ammoInstance = Instantiate(prefab, transform.position, transform.rotation);
            ammoPools[prefab].Enqueue(ammoInstance);
        }

        // Reset its physical state so it snaps exactly to the custom attach transform (to fix offset grabs!)
        Transform attach = socketInteractor.attachTransform != null ? socketInteractor.attachTransform : transform;
        ammoInstance.transform.position = attach.position;
        ammoInstance.transform.rotation = attach.rotation;
        
        Rigidbody rb = ammoInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // FORCE kinematic so it doesn't explode in physics calculations before socket grabs it!
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // MAKE IT ONLINE! 
        // Pre-warmed pools are created in Awake before the network connects. We MUST spawn them now if we are online!
        Unity.Netcode.NetworkObject netObj = ammoInstance.GetComponent<Unity.Netcode.NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            netObj.Spawn();
        }

        // REFILL the magazine so the player doesn't pull out an empty one!
        Magazine mag = ammoInstance.GetComponent<Magazine>();
        if (mag != null)
        {
            mag.Refill();
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
        // The player just pulled the invisible item out of the socket!
        // Instantly turn its renderers back on so they can see the ammo in their hand!
        GameObject grabbedAmmo = args.interactableObject.transform.gameObject;
        SetObjectVisibility(grabbedAmmo, true);

        RefillSocket();
    }

    private void RefillSocket()
    {
        StartCoroutine(RefillSocketRoutine());
    }

    private System.Collections.IEnumerator RefillSocketRoutine()
    {
        if (isRefilling) yield break;
        isRefilling = true;

        yield return new WaitForSeconds(0.1f);

        if (magazinePrefab == null)
        {
            isRefilling = false;
            yield break;
        }

        // --- NEW POOL LOGIC: HIDE INSTEAD OF DESTROY ---
        if (socketInteractor.hasSelection)
        {
            IXRSelectInteractable oldItem = socketInteractor.interactablesSelected[0];
            socketInteractor.interactionManager.SelectCancel((IXRSelectInteractor)socketInteractor, oldItem);
            
            // MULTIPLAYER FIX: Cleanly despawn the old magazine off the network so it doesn't float in mid-air!
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

        // --- NEW POOL LOGIC: FETCH INSTEAD OF INSTANTIATE ---
        GameObject newAmmo = GetAmmoFromPool(magazinePrefab);
        
        // HIDE it immediately so the player thinks the socket is empty!
        SetObjectVisibility(newAmmo, false);

        XRBaseInteractable ammoInteractable = newAmmo.GetComponentInChildren<XRBaseInteractable>(true);

        // Wait 1 frame to guarantee that XR Interaction Toolkit has fully registered the new object!
        yield return new WaitForEndOfFrame();

        if (ammoInteractable != null && socketInteractor.interactionManager != null)
        {
            // Temporarily bypass our own filter to forcefully inject the ammo!
            allowProgrammaticGrab = true;
            socketInteractor.interactionManager.SelectEnter((IXRSelectInteractor)socketInteractor, (IXRSelectInteractable)ammoInteractable);
            allowProgrammaticGrab = false;
            
            // CRITICAL: XRI sometimes moves the object during SelectEnter. 
            // Force it to hide and stay at the attach transform immediately after!
            SetObjectVisibility(newAmmo, false);
            Transform attach = socketInteractor.attachTransform != null ? socketInteractor.attachTransform : transform;
            newAmmo.transform.position = attach.position;
            newAmmo.transform.rotation = attach.rotation;
        }

        isRefilling = false;
    }

    private void SetObjectVisibility(GameObject obj, bool isVisible)
    {
        if (obj == null) return;
        
        // Find all Renderers (MeshRenderers, SkinnedMeshRenderers) on the prefab and its children
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            r.enabled = isVisible;
        }
    }
}
