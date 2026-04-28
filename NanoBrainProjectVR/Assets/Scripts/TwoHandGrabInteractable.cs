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
    }

    private void OnSecondaryRelease(SelectExitEventArgs args)
    {
        Debug.Log("=== SECONDARY GRAB RELEASED ===");
        secondaryInteractor = null;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        // Let the base class (and Grab Transformer) perform the standard position/rotation processing first
        base.ProcessInteractable(updatePhase);

        // During the Dynamic phase, we override the rotation to point the barrel towards the secondary hand
        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
        {
            if (IsTwoHandedGrabbed)
            {
                // The interactor holding the main grip
                IXRSelectInteractor primaryInteractor = interactorsSelecting[0];

                // The point on the weapon where the primary hand is attached
                Transform primaryAttach = GetAttachTransform(primaryInteractor);
                
                // The actual physical positions of the user's controllers
                Transform primaryHand = primaryInteractor.GetAttachTransform(this);
                Transform secondaryHand = secondaryInteractor.GetAttachTransform(secondaryGrip);

                // Calculate the direction vector from the back hand to the front hand
                Vector3 directionToFrontHand = secondaryHand.position - primaryHand.position;

                if (directionToFrontHand.sqrMagnitude > 0.01f)
                {
                    // Target forward orientation aligned with hands, maintaining the primary hand's UP axis
                    Quaternion targetLook = Quaternion.LookRotation(directionToFrontHand.normalized, primaryHand.up);

                    // Compute the primary attach point's local rotation and position differences
                    // so we can properly offset the entire weapon model around the pivot point.
                    Quaternion localRot = Quaternion.Inverse(transform.rotation) * primaryAttach.rotation;
                    Vector3 localPos = transform.InverseTransformPoint(primaryAttach.position);

                    // Re-calculate the root rotation and position applying the inverse local offsets
                    Quaternion finalRot = targetLook * Quaternion.Inverse(localRot);
                    Vector3 finalPos = primaryHand.position - (finalRot * localPos);

                    transform.position = finalPos;
                    transform.rotation = finalRot;

                    // If we have a Rigidbody and aren't using instantaneous movement, 
                    // we need to explicitly tell the physics engine about this override.
                    Rigidbody rb = GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.MovePosition(finalPos);
                        rb.MoveRotation(finalRot);
                        
                        // Clear velocities to stop the physics engine from fighting our manual rotation
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }
    }
}
