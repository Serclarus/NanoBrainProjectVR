using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class AmmoPouch : MonoBehaviour
{
    // Singleton so weapons ALWAYS find the active, real pouch instead of hidden duplicates!
    public static AmmoPouch Instance;

    [Tooltip("The Magazine prefab that this pouch will currently dispense. This is updated dynamically by the WeaponController!")]
    public GameObject magazinePrefab;
    
    private XRSocketInteractor socketInteractor;
    private bool isRefilling = false;

    private void Awake()
    {
        // Enforce singleton so guns never accidentally talk to a hidden duplicate pouch
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple Ammo Pouches detected! Destroying the duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        socketInteractor = GetComponent<XRSocketInteractor>();
        socketInteractor.selectExited.AddListener(OnItemRemovedFromSocket);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (socketInteractor != null)
        {
            socketInteractor.selectExited.RemoveListener(OnItemRemovedFromSocket);
        }
    }

    // Called dynamically by ShotgunController/WeaponController whenever you grab a new weapon!
    public void SetMagazinePrefab(GameObject newMagazinePrefab)
    {
        if (newMagazinePrefab != null && newMagazinePrefab != magazinePrefab)
        {
            magazinePrefab = newMagazinePrefab;
            Debug.Log($"<color=cyan>[AmmoPouch]</color> Updated to dispense: {newMagazinePrefab.name}");
            
            RefillSocket();
        }
        else if (newMagazinePrefab != null && !socketInteractor.hasSelection)
        {
            RefillSocket();
        }
    }

    private void OnItemRemovedFromSocket(SelectExitEventArgs args)
    {
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

        // Destroy the old ammo if we just swapped weapons
        if (socketInteractor.hasSelection)
        {
            IXRSelectInteractable oldItem = socketInteractor.interactablesSelected[0];
            socketInteractor.interactionManager.SelectCancel((IXRSelectInteractor)socketInteractor, oldItem);
            Destroy(oldItem.transform.gameObject);
            
            yield return new WaitForEndOfFrame(); 
        }

        // Spawn the new ammo!
        GameObject newAmmo = Instantiate(magazinePrefab, transform.position, transform.rotation);
        IXRSelectInteractable ammoInteractable = newAmmo.GetComponentInChildren<IXRSelectInteractable>();

        if (ammoInteractable != null && socketInteractor.interactionManager != null)
        {
            socketInteractor.interactionManager.SelectEnter((IXRSelectInteractor)socketInteractor, ammoInteractable);
        }

        isRefilling = false;
    }
}
