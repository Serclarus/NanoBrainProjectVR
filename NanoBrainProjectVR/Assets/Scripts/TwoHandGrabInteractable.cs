using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class TwoHandGrabInteractable : XRGrabInteractable
{
    [Header("Two-Handed Mechanics")]
    [Tooltip("The XR Simple Interactable placed on the front grip of the weapon")]
    public XRSimpleInteractable secondaryGrip;

    private IXRSelectInteractor secondaryInteractor;
    private MovementType originalMovementType;

    /// <summary>
    /// Checks if the object is currently held by both hands.
    /// </summary>
    public bool IsTwoHandedGrabbed => isSelected && interactorsSelecting.Count > 0 && secondaryInteractor != null;

    protected override void Awake()
    {
        base.Awake();
        
        // Listen to secondary grip events
        if (secondaryGrip != null)
        {
            secondaryGrip.selectEntered.AddListener(OnSecondaryGrab);
            secondaryGrip.selectExited.AddListener(OnSecondaryRelease);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        if (secondaryGrip != null)
        {
            secondaryGrip.selectEntered.RemoveListener(OnSecondaryGrab);
            secondaryGrip.selectExited.RemoveListener(OnSecondaryRelease);
        }
    }

    private void OnSecondaryGrab(SelectEnterEventArgs args)
    {
        Debug.Log("=== SECONDARY GRAB DETECTED ===");
        secondaryInteractor = args.interactorObject;
        
        // Temporarily switch to instantaneous movement to stop physics fights
        originalMovementType = movementType;
        movementType = MovementType.Instantaneous;
    }

    private void OnSecondaryRelease(SelectExitEventArgs args)
    {
        Debug.Log("=== SECONDARY GRAB RELEASED ===");
        secondaryInteractor = null;
        
        // Restore original movement type
        movementType = originalMovementType;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        // If the main hand releases the weapon, force the front hand to let go as well
        if (secondaryInteractor != null && secondaryGrip != null)
        {
            interactionManager.SelectCancel(secondaryInteractor, secondaryGrip);
        }
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        // Let the base class (and Grab Transformer) perform the standard position/rotation processing first
        base.ProcessInteractable(updatePhase);

        // Override the position and rotation to strictly lock the weapon between the two hands
        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic || 
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.Fixed ||
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.Late ||
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
        {
            if (IsTwoHandedGrabbed)
            {
                IXRSelectInteractor primaryInteractor = interactorsSelecting[0];
                
                // Get the physical real-world locations of the player's hands
                Transform primaryHand = primaryInteractor.GetAttachTransform(this);
                Transform secondaryHand = secondaryInteractor.GetAttachTransform(secondaryGrip);
                
                // Get the attachment point on the weapon itself
                Transform primaryAttach = GetAttachTransform(primaryInteractor);

                // Find the direction between the two real-world hands
                Vector3 targetWeaponDir = secondaryHand.position - primaryHand.position;
                
                // Find the current direction between the weapon's grips
                Vector3 currentWeaponDir = secondaryGrip.transform.position - primaryAttach.position;

                if (currentWeaponDir.sqrMagnitude > 0.01f && targetWeaponDir.sqrMagnitude > 0.01f)
                {
                    // 1. Rotate the weapon to perfectly align the front grip with the front hand
                    Quaternion rotationDifference = Quaternion.FromToRotation(currentWeaponDir.normalized, targetWeaponDir.normalized);
                    transform.rotation = rotationDifference * transform.rotation;

                    // 2. FORCE the main grip to stay exactly inside the player's primary hand. 
                    // This absolutely guarantees the back hand cannot slide or shift!
                    Vector3 offset = primaryAttach.position - transform.position;
                    transform.position = primaryHand.position - offset;

                    // 3. Update the physics body so it doesn't try to pull the weapon away from us
                    if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Fixed)
                    {
                        Rigidbody rb = GetComponentInParent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.position = transform.position;
                            rb.rotation = transform.rotation;
                            rb.linearVelocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }
                }
            }
        }
    }
}
