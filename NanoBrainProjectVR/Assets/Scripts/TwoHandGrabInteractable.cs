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

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        // Let the base class (and Grab Transformer) perform the standard position/rotation processing first
        base.ProcessInteractable(updatePhase);

        // Override the rotation during all phases to prevent XRI's low-latency updates from snapping it back
        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic || 
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.Fixed ||
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.Late ||
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
        {
            if (IsTwoHandedGrabbed)
            {
                IXRSelectInteractor primaryInteractor = interactorsSelecting[0];
                Transform primaryAttach = GetAttachTransform(primaryInteractor);
                
                // Use the raw controller transform so we don't accidentally get a point that's snapped to the weapon
                Transform secondaryController = secondaryInteractor.transform;

                // The vector from the back hand (pivot) to the front grip on the weapon
                Vector3 currentWeaponDir = secondaryGrip.transform.position - primaryAttach.position;
                
                // The vector from the back hand (pivot) to the player's ACTUAL real-world front hand
                Vector3 targetWeaponDir = secondaryController.position - primaryAttach.position;

                if (currentWeaponDir.sqrMagnitude > 0.01f && targetWeaponDir.sqrMagnitude > 0.01f)
                {
                    // Calculate how much we need to swing the weapon to align the front grip with the front hand
                    Quaternion rotationDifference = Quaternion.FromToRotation(currentWeaponDir.normalized, targetWeaponDir.normalized);

                    // Apply this rotation to the weapon
                    Quaternion finalRot = rotationDifference * transform.rotation;

                    // To pivot around the primary hand, we calculate the offset from the weapon root to the primary hand,
                    // rotate that offset, and apply it back.
                    Vector3 pivotOffset = transform.position - primaryAttach.position;
                    Vector3 rotatedOffset = rotationDifference * pivotOffset;
                    Vector3 finalPos = primaryAttach.position + rotatedOffset;

                    transform.position = finalPos;
                    transform.rotation = finalRot;

                    // Tell the physics engine about this override
                    Rigidbody rb = GetComponentInParent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.MovePosition(finalPos);
                        rb.MoveRotation(finalRot);
                        
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }
    }
}
