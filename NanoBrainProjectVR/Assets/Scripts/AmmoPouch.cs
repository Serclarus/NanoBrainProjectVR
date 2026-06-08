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
        simpleInteractable.hoverEntered.AddListener(OnPouchHovered);
    }

    private void OnDestroy()
    {
        simpleInteractable.selectEntered.RemoveListener(OnPouchGrabbed);
        simpleInteractable.hoverEntered.RemoveListener(OnPouchHovered);
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

    private void OnPouchHovered(HoverEnterEventArgs args)
    {
        Debug.Log($"<color=cyan>[AmmoPouch]</color> SUCCESS: Hand {args.interactorObject.transform.name} hovered over the pouch!");
    }

    // Fallback native physics checks to see if Unity is completely ignoring it
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<XRDirectInteractor>() != null)
        {
            Debug.Log($"<color=yellow>[AmmoPouch]</color> NATIVE PHYSICS: A hand entered the pouch collider, but XRI might not be registering it!");
        }
    }

    private void OnPouchGrabbed(SelectEnterEventArgs args)
    {
        Debug.Log($"<color=green>[AmmoPouch]</color> Pouch Grabbed by {args.interactorObject.transform.name}!");

        if (magazinePrefab == null)
        {
            Debug.LogWarning("AmmoPouch: No magazine prefab assigned to spawn! (You must grab a gun first)");
            return;
        }

        IXRSelectInteractor handInteractor = args.interactorObject;

        // Spawn the exact magazine the player needs right now!
        GameObject newMag = Instantiate(magazinePrefab, transform.position, transform.rotation);
        IXRSelectInteractable magInteractable = newMag.GetComponentInChildren<IXRSelectInteractable>();

        if (magInteractable != null && handInteractor != null)
        {
            // Safely swap the grab on the next frame to prevent locking the XR Interaction Manager
            StartCoroutine(ForceGrabRoutine(handInteractor, magInteractable));
        }
    }

    private System.Collections.IEnumerator ForceGrabRoutine(IXRSelectInteractor hand, IXRSelectInteractable mag)
    {
        // Wait for the interaction manager to finish processing the Pouch's current grab event!
        yield return new WaitForEndOfFrame();
        
        if (simpleInteractable.interactionManager != null)
        {
            // Force the hand to let go of the invisible vest pouch...
            simpleInteractable.interactionManager.SelectCancel(hand, simpleInteractable);
            
            // ...and instantly grab the brand new magazine we just spawned!
            simpleInteractable.interactionManager.SelectEnter(hand, mag);
            Debug.Log($"<color=green>[AmmoPouch]</color> Successfully spawned and handed {mag.transform.name} to the player!");
        }
    }
}
