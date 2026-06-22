using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[System.Serializable]
public struct AmmoPoolConfig
{
    public GameObject ammoPrefab;
    [Tooltip("How many to pre-instantiate at the start of the game.")]
    public int initialPoolSize;
}

[RequireComponent(typeof(XRSocketInteractor))]
public class AmmoPouch : MonoBehaviour
{
    [Tooltip("The Magazine prefab that this pouch will currently dispense. This is updated dynamically by the WeaponController!")]
    public GameObject magazinePrefab;
    
    [Header("Circular Object Pool")]
    [Tooltip("Configure which magazines to pre-instantiate and how many to keep in the circular pool. (e.g. 4 pistol, 4 rifle, 7 shotgun shells)")]
    public AmmoPoolConfig[] prewarmedPools;

    private Dictionary<GameObject, Queue<GameObject>> ammoPools = new Dictionary<GameObject, Queue<GameObject>>();

    private XRSocketInteractor socketInteractor;
    private bool isRefilling = false;

    private void Awake()
    {
        socketInteractor = GetComponent<XRSocketInteractor>();
        if (socketInteractor != null)
        {
            socketInteractor.selectExited.AddListener(OnItemRemovedFromSocket);
        }

        InitializePools();
    }

    private void OnDestroy()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectExited.RemoveListener(OnItemRemovedFromSocket);
        }
    }

    private void InitializePools()
    {
        // Create a hidden parent object to keep the hierarchy extremely clean
        // We leave this at the ROOT of the scene, otherwise dropped magazines will move with the player!
        Transform poolParent = new GameObject("AmmoPouch_Pool").transform;

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
            ammoPools[prefab].Enqueue(candidate); // Keep the circle going

            if (candidate == null) continue;

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

        // Reset its physical state so it doesn't fly out of the socket!
        ammoInstance.transform.position = transform.position;
        ammoInstance.transform.rotation = transform.rotation;
        
        Rigidbody rb = ammoInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
            
            // Instantly hide it! It's already in the circular queue, so it will be automatically reused!
            oldItem.transform.gameObject.SetActive(false);
            
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
            socketInteractor.interactionManager.SelectEnter((IXRSelectInteractor)socketInteractor, (IXRSelectInteractable)ammoInteractable);
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
