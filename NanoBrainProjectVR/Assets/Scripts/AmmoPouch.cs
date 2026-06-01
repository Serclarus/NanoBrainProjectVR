using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSimpleInteractable))]
public class AmmoPouch : MonoBehaviour
{
    [Tooltip("The Magazine prefab that this pouch will currently dispense. This is updated dynamically by the WeaponController!")]
    public GameObject magazinePrefab;
    
    private XRSimpleInteractable simpleInteractable;

    private void Awake()
    {
        simpleInteractable = GetComponent<XRSimpleInteractable>();
        simpleInteractable.selectEntered.AddListener(OnPouchGrabbed);
    }

    private void OnDestroy()
    {
        simpleInteractable.selectEntered.RemoveListener(OnPouchGrabbed);
    }

    // Called dynamically by WeaponController.cs whenever you grab a new weapon!
    public void SetMagazinePrefab(GameObject newMagazinePrefab)
    {
        if (newMagazinePrefab != null)
        {
            magazinePrefab = newMagazinePrefab;
            Debug.Log($"Ammo Pouch updated to dispense: {newMagazinePrefab.name}");
        }
    }

    private void OnPouchGrabbed(SelectEnterEventArgs args)
    {
        if (magazinePrefab == null)
        {
            Debug.LogWarning("AmmoPouch: No magazine prefab assigned to spawn!");
            return;
        }

        IXRSelectInteractor handInteractor = args.interactorObject;

        // Spawn the exact magazine the player needs right now!
        GameObject newMag = Instantiate(magazinePrefab, transform.position, transform.rotation);
        IXRSelectInteractable magInteractable = newMag.GetComponentInChildren<IXRSelectInteractable>();

        if (magInteractable != null && handInteractor != null)
        {
            // Force the hand to let go of the invisible vest pouch...
            simpleInteractable.interactionManager.SelectCancel(handInteractor, simpleInteractable);
            
            // ...and instantly grab the brand new magazine we just spawned!
            simpleInteractable.interactionManager.SelectEnter(handInteractor, magInteractable);
        }
    }
}
