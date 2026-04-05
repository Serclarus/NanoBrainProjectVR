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
    [Tooltip("Assign the bullet trail/tracer particle system here")]
    public ParticleSystem bulletTrail; 

    [Header("Recoil Visuals")]
    [Tooltip("How much the gun rotates upward/backward per shot")]
    public Vector3 recoilKick = new Vector3(-10f, 0, 0); 
    [Tooltip("How fast the gun returns to the rest position")]
    public float returnSpeed = 5f;
    
    private Vector3 originalLocalRotation;
    private Vector3 currentRecoilOffset = Vector3.zero;

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
        // Store the resting rotation of the gun relative to the hand
        originalLocalRotation = transform.localEulerAngles;
    }

    private void Update()
    {
        // Smoothly return gun rotation to normal over time
        currentRecoilOffset = Vector3.Lerp(currentRecoilOffset, Vector3.zero, Time.deltaTime * returnSpeed);
        transform.localEulerAngles = originalLocalRotation + currentRecoilOffset;
    }

    // Call this function when the VR player pulls the trigger
    public void FireWeapon()
    {
        Debug.Log("FireWeapon called!");
        if (barrelPoint == null)
        {
            Debug.LogWarning("WeaponController: No barrel point assigned! Please create an empty GameObject at the barrel tip and assign it.");
            return;
        }

        // 1. Visuals: Play the attached Particle Systems for flash & trail
        if (muzzleFlash != null) muzzleFlash.Play();
        if (bulletTrail != null) bulletTrail.Play();

        // 2. Physics/Network: Apply Recoil Kick
        currentRecoilOffset += recoilKick;

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

            // Note: Add logic here later for hitting the Boar AI
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
