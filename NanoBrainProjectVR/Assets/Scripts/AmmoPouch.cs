using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class AmmoPouch : MonoBehaviour
{
    [Tooltip("The Magazine prefab that this pouch will currently dispense. This is updated dynamically by the WeaponController!")]
    public GameObject magazinePrefab;
    
    private XRSocketInteractor socketInteractor;
    private bool isRefilling = false;

    private void Awake()
    {
        socketInteractor = GetComponent<XRSocketInteractor>();
        if (socketInteractor != null)
        {
            socketInteractor.selectExited.AddListener(OnItemRemovedFromSocket);
        }
    }

    private void OnDestroy()
    {
        if (socketInteractor != null)
        {
            socketInteractor.selectExited.RemoveListener(OnItemRemovedFromSocket);
        }
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
        Debug.Log($"<color=white>[AmmoPouch]</color> Refill Routine Started!");
        if (isRefilling) 
        {
            Debug.Log($"<color=white>[AmmoPouch]</color> Aborted: Already refilling.");
            yield break;
        }
        isRefilling = true;

        yield return new WaitForSeconds(0.1f);

        if (magazinePrefab == null)
        {
            Debug.Log($"<color=white>[AmmoPouch]</color> Aborted: magazinePrefab is completely null!");
            isRefilling = false;
            yield break;
        }

        // Destroy the old ammo if we just swapped weapons
        if (socketInteractor.hasSelection)
        {
            Debug.Log($"<color=white>[AmmoPouch]</color> Destroying old item in socket...");
            IXRSelectInteractable oldItem = socketInteractor.interactablesSelected[0];
            socketInteractor.interactionManager.SelectCancel((IXRSelectInteractor)socketInteractor, oldItem);
            Destroy(oldItem.transform.gameObject);
            
            yield return new WaitForEndOfFrame(); 
        }

        Debug.Log($"<color=white>[AmmoPouch]</color> Instantiating new {magazinePrefab.name}...");
        // Spawn the new ammo!
        GameObject newAmmo = Instantiate(magazinePrefab, transform.position, transform.rotation);
        
        // HIDE it immediately so the player thinks the socket is empty!
        SetObjectVisibility(newAmmo, false);

        XRBaseInteractable ammoInteractable = newAmmo.GetComponentInChildren<XRBaseInteractable>(true);

        Debug.Log($"<color=white>[AmmoPouch]</color> Waiting for XR Interaction Toolkit to register...");
        // Wait 1 frame to guarantee that XR Interaction Toolkit has fully registered the new object!
        yield return new WaitForEndOfFrame();

        if (ammoInteractable != null)
        {
            if (socketInteractor.interactionManager != null)
            {
                socketInteractor.interactionManager.SelectEnter((IXRSelectInteractor)socketInteractor, (IXRSelectInteractable)ammoInteractable);
                Debug.Log($"<color=green>[AmmoPouch]</color> Successfully spawned invisible {magazinePrefab.name} into the socket!");
            }
            else
            {
                Debug.LogError($"<color=red>[AmmoPouch]</color> FAILED! The Socket is missing an Interaction Manager!");
            }
        }
        else
        {
            Debug.LogError($"<color=red>[AmmoPouch]</color> FAILED! The spawned ammo ({magazinePrefab.name}) does NOT have an XRGrabInteractable component on it! The pouch cannot grab it.");
        }

        isRefilling = false;
        Debug.Log($"<color=white>[AmmoPouch]</color> Refill Routine Finished!");
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
