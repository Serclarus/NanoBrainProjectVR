using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

[RequireComponent(typeof(XRSocketInteractor))]
[Tooltip("Attach this to a Holster Socket to restrict it to only accepting a specific WeaponSlotType.")]
public class WeaponSocketFilter : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
{
    [Tooltip("The only type of weapon this socket is allowed to hold.")]
    public WeaponSlotType allowedWeaponType;

    // Required by XRI filter interfaces
    public bool canProcess => true;

    private XRSocketInteractor socket;

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        
        // Dynamically inject this filter into the Socket's validation system
        if (socket != null)
        {
            socket.hoverFilters.Add(this);
            socket.selectFilters.Add(this);
        }
    }

    private void OnDestroy()
    {
        if (socket != null)
        {
            socket.hoverFilters.Remove(this);
            socket.selectFilters.Remove(this);
        }
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return IsWeaponAllowed(interactable);
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return IsWeaponAllowed(interactable);
    }

    private bool IsWeaponAllowed(IXRInteractable interactable)
    {
        // Try to find the WeaponAutoReturn script on the object trying to enter the socket
        if (interactable.transform.TryGetComponent<WeaponAutoReturn>(out var weapon))
        {
            // 1. Only allow it if the slot types match perfectly!
            if (weapon.slotType != allowedWeaponType) return false;

            // 2. MULTIPLAYER THEFT PROTECTION:
            // Prevent another player's holster from stealing this weapon if they stand too close!
            // The weapon's Network Owner must exactly match the Holster's Network Owner.
            Unity.Netcode.NetworkObject weaponNetObj = weapon.GetComponent<Unity.Netcode.NetworkObject>();
            Unity.Netcode.NetworkObject holsterNetObj = GetComponentInParent<Unity.Netcode.NetworkObject>();
            
            if (weaponNetObj != null && holsterNetObj != null)
            {
                if (weaponNetObj.OwnerClientId != holsterNetObj.OwnerClientId)
                {
                    // This weapon belongs to another player! Reject it!
                    return false;
                }
            }

            return true;
        }
        
        // If the object doesn't even have a WeaponAutoReturn script (e.g. ammo, magazines), reject it!
        return false;
    }
}
