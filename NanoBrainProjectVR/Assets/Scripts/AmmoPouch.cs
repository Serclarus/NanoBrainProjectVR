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
        socketInteractor.selectExited.AddListener(OnItemRemovedFromSocket);
    }

    private void OnDestroy()
    {
        socketInteractor.selectExited.RemoveListener(OnItemRemovedFromSocket);
    }

    // Called dynamically by ShotgunController/WeaponController whenever you grab a new weapon!
    public void SetMagazinePrefab(GameObject newMagazinePrefab)
    {
        if (newMagazinePrefab != null && newMagazinePrefab != magazinePrefab)
        {
            magazinePrefab = newMagazinePrefab;
            Debug.Log($"Ammo Pouch updated to dispense: {newMagazinePrefab.name}");
            
            // The weapon changed! Throw away the old ammo and spawn the correct one.
            RefillSocket();
        }
        else if (newMagazinePrefab != null && !socketInteractor.hasSelection)
        {
            // If it's the exact same weapon ammo, but the socket is currently empty, refill it!
            RefillSocket();
        }
    }

    private void OnItemRemovedFromSocket(SelectExitEventArgs args)
    {
        // When the player physically grabs the ammo out of the socket, refill it!
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

        // Wait a tiny fraction of a second to let the player's hand completely clear the interaction manager
        yield return new WaitForSeconds(0.1f);

        if (magazinePrefab == null)
        {
            isRefilling = false;
            yield break;
        }

        // If there is currently the wrong ammo (or an old item) sitting in the socket, destroy it!
        if (socketInteractor.hasSelection)
        {
            IXRSelectInteractable oldItem = socketInteractor.interactablesSelected[0];
            socketInteractor.interactionManager.SelectCancel((IXRSelectInteractor)socketInteractor, oldItem);
            Destroy(oldItem.transform.gameObject);
            
            yield return new WaitForEndOfFrame(); // Wait for destruction to clear the physics engine
        }

        // Spawn the brand new ammo!
        GameObject newAmmo = Instantiate(magazinePrefab, transform.position, transform.rotation);
        IXRSelectInteractable ammoInteractable = newAmmo.GetComponentInChildren<IXRSelectInteractable>();

        if (ammoInteractable != null && socketInteractor.interactionManager != null)
        {
            // Force the socket to instantly grab the newly spawned ammo so it sits nicely on the player's chest
            socketInteractor.interactionManager.SelectEnter((IXRSelectInteractor)socketInteractor, ammoInteractable);
            Debug.Log($"<color=green>[AmmoPouch]</color> Socket automatically restocked with {magazinePrefab.name}!");
        }

        isRefilling = false;
    }
}
