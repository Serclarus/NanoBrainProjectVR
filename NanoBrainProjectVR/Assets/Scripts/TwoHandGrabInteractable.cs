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
        secondaryInteractor = args.interactorObject;
    }

    private void OnSecondaryRelease(SelectExitEventArgs args)
    {
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
                    transform.rotation = targetLook * Quaternion.Inverse(localRot);
                    transform.position = primaryHand.position - (transform.rotation * localPos);
                }
            }
        }
    }
}
