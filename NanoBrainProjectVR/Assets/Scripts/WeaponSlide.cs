using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class WeaponSlide : MonoBehaviour
{
    [Tooltip("The WeaponController script that fires the RackSlide method and owns the bolt visual.")]
    public WeaponController weapon;

    [Tooltip("How far the slide needs to be pulled back to chamber a round (should match WeaponController's boltTravelDistance).")]
    public float rackDistance = 0.05f;

    [Tooltip("Usually the local Z axis is standard (0,0,-1) or (0,0,1) depending on model orientation.")]
    public Vector3 pullAxis = new Vector3(0, 0, 1);

    private XRSimpleInteractable interactable;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor currentInteractor;
    
    private bool isGrabbed = false;
    private bool hasRackedThisPull = false;

    // We store the hand's initial offset to calculate dragging smoothly
    private Vector3 grabStartLocalPos;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        hasRackedThisPull = false;
        currentInteractor = args.interactorObject;

        if (weapon != null)
        {
            weapon.isSlideGrabbed = true;
            // Record the hand's local position relative to the weapon when first grabbed
            grabStartLocalPos = weapon.transform.InverseTransformPoint(currentInteractor.transform.position);
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        isGrabbed = false;
        currentInteractor = null;

        if (weapon != null)
        {
            weapon.isSlideGrabbed = false;
            // Target offset will snap back to 0 procedurally in WeaponController
        }
    }

    private void Update()
    {
        if (isGrabbed && currentInteractor != null && weapon != null)
        {
            // Calculate where the hand is now in the weapon's local space
            Vector3 currentHandLocalPos = weapon.transform.InverseTransformPoint(currentInteractor.transform.position);
            
            // The difference along the specified axis
            // Project the hand movement onto the pull axis
            Vector3 handMovement = currentHandLocalPos - grabStartLocalPos;
            float movementAmount = Vector3.Dot(handMovement, pullAxis.normalized);

            // Clamp between 0 (resting) and rackDistance (fully pulled)
            float clampedPull = Mathf.Clamp(movementAmount, 0f, rackDistance);

            // Directly drive the WeaponController's procedural variables!
            weapon.targetBoltOffset = clampedPull;
            weapon.currentBoltOffset = clampedPull;

            // If we pull it more than 90% of the way, rack the gun!
            if (!hasRackedThisPull && clampedPull >= rackDistance * 0.9f)
            {
                hasRackedThisPull = true;
                weapon.RackSlide();
            }
        }
    }
}
