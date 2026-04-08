using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // Required for XR Grab Interactable

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class WeaponController : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

    [Header("Shooting Setup")]
    public float range = 100f;
    [Tooltip("Which layers should the bullet hit?")]
    public LayerMask hitMask = ~0; 
    [Tooltip("The point where the raycast bullet originates")]
    public Transform barrelPoint;

    [Header("Visual Effects")]
    [Tooltip("Assign the generic muzzle flash particle system here")]
    public ParticleSystem muzzleFlash; 

    [Header("Procedural Recoil Settings")]
    [Tooltip("Assign the visual model wrapper of the gun here. Rotating the root will conflict with VR Tracking!")]
    public Transform weaponModel;

    [Header("Recoil - Positional Kick")]
    [Tooltip("How far the gun physically kicks back towards the player shoulder per shot")]
    public float backwardKick = 0.05f; 

    [Header("Recoil - Rotational Kick")]
    [Tooltip("Min and Max upward kick (Pitch)")]
    public Vector2 recoilPitchRange = new Vector2(4f, 8f);
    [Tooltip("Side to side wobble per shot (Yaw)")]
    public Vector2 recoilYawRange = new Vector2(-2f, 2f);
    [Tooltip("Rotational twist per shot (Roll)")]
    public Vector2 recoilRollRange = new Vector2(-1f, 1f);

    [Header("Recoil - Feel (Dynamics)")]
    [Tooltip("How fast the gun snaps to the peak of the recoil. High values feel punchy and sharp.")]
    public float snappiness = 20f;
    [Tooltip("How slow the gun recovers back to rest position. Low values feel heavy.")]
    public float returnSpeed = 8f;
    
    private Vector3 originalModelRotation;
    private Vector3 originalModelPosition;

    // Variables for procedural spring math
    private Vector3 currentRotation;
    private Vector3 targetRotation;
    private Vector3 currentPosition;
    private Vector3 targetPosition;

    private void Awake()
    {
        grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        // Auto-subscribe to the trigger pull event!
        if (grabInteractable != null)
        {
            grabInteractable.activated.AddListener(OnTriggerPulled);
        }
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.activated.RemoveListener(OnTriggerPulled);
        }
    }

    private void OnTriggerPulled(ActivateEventArgs args)
    {
        // This is called instantly when you pull the controller trigger while holding the weapon
        FireWeapon();
    }

    private void Start()
    {
        // Store the resting position/rotation of the visual model
        if (weaponModel != null)
        {
            originalModelRotation = weaponModel.localEulerAngles;
            originalModelPosition = weaponModel.localPosition;
        }
        else
        {
            Debug.LogWarning("WeaponController: No 'Weapon Model' assigned! Recoil animation will not play. Please assign a visual child object.");
        }
    }

    private void Update()
    {
        if (weaponModel != null)
        {
            // 1. The target variables steadily recover back to zero (neutral state)
            targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, Time.deltaTime * returnSpeed);
            targetPosition = Vector3.Lerp(targetPosition, Vector3.zero, Time.deltaTime * returnSpeed);

            // 2. The current visible position snaps sharply towards the targets
            currentRotation = Vector3.Slerp(currentRotation, targetRotation, Time.deltaTime * snappiness);
            currentPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * snappiness);

            // 3. Apply variations to the model
            weaponModel.localEulerAngles = originalModelRotation + currentRotation;
            weaponModel.localPosition = originalModelPosition + currentPosition;
        }
    }

    // Call this function when the VR player pulls the trigger
    public void FireWeapon()
    {
        if (barrelPoint == null)
        {
            Debug.LogWarning("WeaponController: No barrel point assigned! Please create an empty GameObject at the barrel tip and assign it.");
            return;
        }

        // 1. Visuals: Play the attached Particle Systems for flash & trail
        if (muzzleFlash != null)
        {
            muzzleFlash.gameObject.SetActive(true);
            // Stop and clear forces the burst emitter to fire again immediately!
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play(true);
        }

        // 2. Procedural Recoil Integration: Define the randomized strength of the current shot
        if (weaponModel != null)
        {
            float pitch = Random.Range(recoilPitchRange.x, recoilPitchRange.y); // Positive X since the model has a reversed origin
            float yaw = Random.Range(recoilYawRange.x, recoilYawRange.y);
            float roll = Random.Range(recoilRollRange.x, recoilRollRange.y);

            // Add onto the current recoil target rather than replacing it, causing recoil stacking for rapid fire!
            targetRotation += new Vector3(pitch, yaw, roll);
            targetPosition += new Vector3(0, 0, -backwardKick);
        }

        // 3. Logic: Raycast Hit Detection
        if (Physics.Raycast(barrelPoint.position, barrelPoint.forward, out RaycastHit hit, range, hitMask))
        {
            // 4. Hit Logic (Highly Optimized: Single GetComponent call)
            SurfaceType type = SurfaceType.Default;
            HittableSurface hittable = hit.collider.GetComponentInParent<HittableSurface>();
            
            if (hittable != null)
            {
                // Read the material type for the particles
                type = hittable.surfaceType;
                
                // Trigger the damage/score logic
                hittable.OnHit();
            }

            // Spawn hit effect from the pool precisely on the surface
            if (HitEffectPoolManager.Instance != null)
            {
                HitEffectPoolManager.Instance.SpawnHitEffect(hit.point, hit.normal, type);
            }
        }
    }

    // Editor Helper Method: Allows you to right-click the script in Unity Editor -> "Test Fire Gun"
    // so you can test shooting mechanics without wearing the VR headset!
    [ContextMenu("Test Fire Gun")]
    public void DebugFire()
    {
        FireWeapon();
        Debug.Log("Debug Fire Triggered!");
    }
}
