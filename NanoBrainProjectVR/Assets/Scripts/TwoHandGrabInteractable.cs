using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class TwoHandGrabInteractable : XRGrabInteractable
{
    public enum TwoHandBehavior { Aimable, StabilizeOnly, PumpAction }

    [Header("Two-Handed Mechanics")]
    [Tooltip("The behavior of the secondary hand. Aimable (Rifle), StabilizeOnly (Pistol), PumpAction (Shotgun)")]
    public TwoHandBehavior twoHandBehavior = TwoHandBehavior.Aimable;

    [Tooltip("The XR Simple Interactable placed on the front grip of the weapon")]
    public XRSimpleInteractable secondaryGrip;

    [Header("Pump Action Settings (Shotgun Only)")]
    [Tooltip("The physical pump object that slides back and forth (Usually the parent of the secondary grip)")]
    public Transform pumpSlideTransform;
    [Tooltip("Local Z position of the pump when pushed all the way forward")]
    public float pumpForwardZ = 0.2f;
    [Tooltip("Local Z position of the pump when pulled all the way back")]
    public float pumpBackZ = 0.0f;
    [Tooltip("Deadzone distance. The hand must move this far before the pump slides, preventing jitter while aiming.")]
    public float pumpDeadzone = 0.02f;
    [Tooltip("If true, the hand will automatically let go of the pump grip as soon as the pump action is completed.")]
    public bool autoReleasePumpOnComplete = false;

    [Tooltip("Optional: The physical bolt object. Unparent it from the pump so they are siblings!")]
    public Transform boltTransform;
    [Tooltip("Multiplier for how much the bolt moves compared to the pump (e.g. 0.5 moves half as much).")]
    public float boltMovementMultiplier = 0.5f;
    private float initialBoltZ;

    public UnityEngine.Events.UnityEvent OnPumpPulledBack;
    public UnityEngine.Events.UnityEvent OnPumpPushedForward;

    private IXRSelectInteractor secondaryInteractor;
    private MovementType originalMovementType;

    private bool hasPumpedBack = false;
    private bool hasPumpedForward = true; // Assume it starts forward!

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

        if (boltTransform != null)
        {
            initialBoltZ = boltTransform.localPosition.z;
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
        
        // Temporarily switch to instantaneous movement to stop physics fights
        originalMovementType = movementType;
        movementType = MovementType.Instantaneous;
    }

    private void OnSecondaryRelease(SelectExitEventArgs args)
    {
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

        // Override the rotation during all phases to prevent XRI's low-latency updates from snapping it back
        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic || 
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.Fixed ||
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.Late ||
            updatePhase == XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
        {
            if (IsTwoHandedGrabbed)
            {
                // --- PISTOL LOGIC ---
                // If it is just stabilizing, we DO NOT override the rotation. Let the primary hand handle it!
                if (twoHandBehavior == TwoHandBehavior.StabilizeOnly)
                {
                    return; 
                }

                IXRSelectInteractor primaryInteractor = interactorsSelecting[0];
                Transform secondaryController = secondaryInteractor.transform;
                Vector3 truePivot = primaryInteractor.transform.position;

                // --- SHOTGUN PUMP LOGIC ---
                if (twoHandBehavior == TwoHandBehavior.PumpAction && pumpSlideTransform != null)
                {
                    // Find where the player's front hand is along the local Z axis of the gun!
                    Vector3 localHandPos = transform.InverseTransformPoint(secondaryController.position);
                    
                    float currentZ = pumpSlideTransform.localPosition.z;
                    float minZ = Mathf.Min(pumpForwardZ, pumpBackZ);
                    float maxZ = Mathf.Max(pumpForwardZ, pumpBackZ);
                    float clampedZ = Mathf.Clamp(localHandPos.z, minZ, maxZ);

                    // Actually slide the pump object!
                    pumpSlideTransform.localPosition = new Vector3(
                        pumpSlideTransform.localPosition.x, 
                        pumpSlideTransform.localPosition.y, 
                        clampedZ
                    );

                    // Move the bolt proportionally!
                    if (boltTransform != null)
                    {
                        float pumpTravelDistance = clampedZ - pumpForwardZ;
                        float scaledBoltTravel = pumpTravelDistance * boltMovementMultiplier;

                        boltTransform.localPosition = new Vector3(
                            boltTransform.localPosition.x,
                            boltTransform.localPosition.y,
                            initialBoltZ + scaledBoltTravel
                        );
                    }

                    // Fire events when it hits the limits!
                    float pumpThreshold = 0.035f;
                    if (Mathf.Abs(clampedZ - pumpBackZ) < pumpThreshold && !hasPumpedBack)
                    {
                        hasPumpedBack = true;
                        hasPumpedForward = false;
                        OnPumpPulledBack?.Invoke();
                    }
                    else if (Mathf.Abs(clampedZ - pumpForwardZ) < pumpThreshold && !hasPumpedForward)
                    {
                        hasPumpedForward = true;
                        hasPumpedBack = false;
                        OnPumpPushedForward?.Invoke();

                        // Automatically force the player to let go of the pump grip if enabled
                        if (autoReleasePumpOnComplete && secondaryInteractor != null && secondaryGrip != null)
                        {
                            interactionManager.SelectCancel(secondaryInteractor, secondaryGrip);
                        }
                    }
                }

                // --- AIMABLE LOGIC (RIFLES AND SHOTGUNS) ---
                // The vector from the back hand (pivot) to the front grip on the weapon
                Vector3 currentWeaponDir = secondaryGrip.transform.position - truePivot;
                
                // The vector from the back hand (pivot) to the player's ACTUAL real-world front hand
                Vector3 targetWeaponDir = secondaryController.position - truePivot;

                if (currentWeaponDir.sqrMagnitude > 0.01f && targetWeaponDir.sqrMagnitude > 0.01f)
                {
                    // Calculate how much we need to swing the weapon to align the front grip with the front hand
                    Quaternion rotationDifference = Quaternion.FromToRotation(currentWeaponDir.normalized, targetWeaponDir.normalized);

                    // Apply this rotation to the weapon
                    Quaternion finalRot = rotationDifference * transform.rotation;

                    // Pivot around the true controller position
                    Vector3 pivotOffset = transform.position - truePivot;
                    Vector3 rotatedOffset = rotationDifference * pivotOffset;
                    Vector3 finalPos = truePivot + rotatedOffset;

                    transform.position = finalPos;
                    transform.rotation = finalRot;

                    // Tell the physics engine about this override
                    Rigidbody rb = GetComponentInParent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.MovePosition(finalPos);
                        rb.MoveRotation(finalRot);
                        
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }
    }
}
