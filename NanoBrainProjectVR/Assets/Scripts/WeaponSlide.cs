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

    [Header("Audio (Optional)")]
    [Tooltip("Leave empty to automatically use the WeaponController's AudioSource")]
    public AudioSource audioSource;
    public AudioClip slideBackSound;
    public AudioClip slideForwardSound;
    [Range(0f, 1f)] public float volume = 1f;

    private XRSimpleInteractable interactable;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor currentInteractor;
    
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

    private float initialBoltOffset = 0f;

    private void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        hasRackedThisPull = false;
        currentInteractor = args.interactorObject;

        if (weapon != null)
        {
            weapon.isSlideGrabbed = true;
            // Record the current visual offset of the bolt so we don't snap to 0!
            initialBoltOffset = weapon.currentBoltOffset;
            
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

            // If we had successfully racked it this pull, play the forward snap sound!
            if (hasRackedThisPull)
            {
                PlaySound(slideForwardSound);
            }
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource sourceToUse = audioSource;
        if (sourceToUse == null && weapon != null)
        {
            sourceToUse = weapon.audioSource;
        }

        if (sourceToUse != null)
        {
            sourceToUse.PlayOneShot(clip, volume);
        }
    }

    private void Update()
    {
        if (isGrabbed && currentInteractor != null && weapon != null)
        {
            // Calculate where the hand is now in the weapon's local space
            Vector3 currentHandLocalPos = weapon.transform.InverseTransformPoint(currentInteractor.transform.position);
            
            // Project the hand movement onto the pull axis
            Vector3 handMovement = currentHandLocalPos - grabStartLocalPos;
            float movementAmount = Vector3.Dot(handMovement, pullAxis.normalized);

            // Add the hand movement to whatever position the bolt was already at!
            float newBoltOffset = initialBoltOffset + movementAmount;

            // Clamp between 0 (resting) and slightly past rackDistance (to allow unlocking)
            float clampedPull = Mathf.Clamp(newBoltOffset, 0f, rackDistance * 1.05f);

            // Directly drive the WeaponController's procedural variables (visually capped slightly past max rack so it moves when locked!)
            weapon.targetBoltOffset = Mathf.Min(clampedPull, rackDistance * 1.1f);
            weapon.currentBoltOffset = Mathf.Min(clampedPull, rackDistance * 1.1f);

            // Logic changes depending on whether the slide was locked back!
            if (weapon.isSlideLockedBack.Value)
            {
                // If the slide is locked back, they only need to pull it slightly further backwards from where they grabbed it!
                float unlockThreshold = initialBoltOffset + 0.01f; 
                
                if (!hasRackedThisPull && newBoltOffset >= unlockThreshold)
                {
                    hasRackedThisPull = true;
                    weapon.isSlideLockedBack.Value = false; // Unlock it!
                    weapon.RackSlide(); // Racking it when unlocked will chamber a new round if magazine has ammo

                    PlaySound(slideBackSound);

                    // Force release the hand so it doesn't hold the bolt forever
                    if (interactable.interactionManager != null && currentInteractor != null)
                    {
                        interactable.interactionManager.SelectCancel(currentInteractor, interactable);
                    }
                }
            }
            else
            {
                // If it's NOT locked back, they must pull it almost all the way to rack it!
                if (!hasRackedThisPull && clampedPull >= rackDistance * 0.9f)
                {
                    hasRackedThisPull = true;
                    weapon.RackSlide();

                    PlaySound(slideBackSound);

                    // Force release the hand so it doesn't hold the bolt forever
                    if (interactable.interactionManager != null && currentInteractor != null)
                    {
                        interactable.interactionManager.SelectCancel(currentInteractor, interactable);
                    }
                }
            }
        }
    }
}
