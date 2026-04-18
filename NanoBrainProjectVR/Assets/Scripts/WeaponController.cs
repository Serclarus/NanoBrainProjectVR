using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit; // Required for XR Grab Interactable
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Required for XRSocketInteractor

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

    [Header("Fire Mode")]
    [Tooltip("If true, holding the trigger will fire continuously. If false, one shot per trigger pull.")]
    public bool fullAuto = true;
    [Tooltip("Rounds per minute for full-auto fire")]
    public float fireRate = 600f;
    private float fireCooldownTimer = 0f;
    private bool isTriggerHeld = false;
    private bool isHeld = false;

    [Header("Trigger Animation")]
    [Tooltip("The trigger bone/transform on the weapon model")]
    public Transform triggerTransform;
    [Tooltip("Maximum rotation angle when the trigger is fully pulled (degrees). Positive = rotates on local X since the asset is backwards.")]
    public float triggerMaxAngle = 15f;
    [Tooltip("The axis around which the trigger rotates locally")]
    public Vector3 triggerRotateAxis = Vector3.right;
    private Quaternion triggerOriginalRotation;
    private XRBaseInputInteractor currentHoldingInteractor;

    [Header("Ammo & Reloading Setup")]
    [Tooltip("The socket interactor that holds the magazine")]
    public XRSocketInteractor magazineSocket;
    
    private Magazine currentMagazine;
    private bool isChambered = false;

    [Header("Visual Effects")]
    [Tooltip("Assign the generic muzzle flash particle system here")]
    public ParticleSystem muzzleFlash; 

    [Header("Audio Settings")]
    [Tooltip("The audio clip played when the weapon fires")]
    public AudioClip shootSound;
    [Tooltip("Sound to play when pulling the trigger but there's no ammo chambered")]
    public AudioClip dryFireSound;
    [Tooltip("The AudioSource component used to play the sound")]
    public AudioSource audioSource;
    [Tooltip("Slightly randomize the pitch of each shot so it doesn't sound repetitive")]
    public Vector2 soundPitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("Volume of the gunshot")]
    [Range(0f, 1f)] public float shootVolume = 1f;

    [Header("Controller Haptics")]
    [Tooltip("How strong the controller vibrates (0.0 to 1.0)")]
    [Range(0f, 1f)] public float hapticIntensity = 0.5f;
    [Tooltip("How long the controller vibrates in seconds")]
    public float hapticDuration = 0.1f;

    [Header("Bolt Animation & Shell Ejection")]
    [Tooltip("The moving part of the weapon (the bolt or slide)")]
    public Transform boltTransform;
    [Tooltip("How far the bolt moves backwards on the Z axis")]
    public float boltTravelDistance = 0.05f;
    [Tooltip("How fast the bolt snaps back")]
    public float boltSnappiness = 30f;
    [Tooltip("How fast the bolt springs forward")]
    public float boltReturnSpeed = 15f;

    [Tooltip("The point where shells are ejected from")]
    public Transform shellEjectionPoint;
    [Tooltip("The empty shell prefab to instantiate")]
    public GameObject shellPrefab;
    [Tooltip("How many shells to keep in the pool")]
    public int shellPoolSize = 15;
    [Tooltip("How long a shell lives before being disabled")]
    public float shellLifeTime = 4f;
    [Tooltip("Force applied to the ejected shell")]
    public float shellEjectionForce = 3f;
    [Tooltip("Spin applied to the ejected shell")]
    public float shellTorque = 1f;

    [HideInInspector] public Vector3 originalBoltPosition;
    [HideInInspector] public float currentBoltOffset;
    [HideInInspector] public float targetBoltOffset;
    [HideInInspector] public bool isSlideGrabbed = false;
    private bool hasEjectedShell = true;

    private Queue<GameObject> shellPool;
    private Dictionary<GameObject, Coroutine> activeShellCoroutines;

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
        
        if (grabInteractable != null)
        {
            // Track when the weapon is picked up / dropped
            grabInteractable.selectEntered.AddListener(OnWeaponGrabbed);
            grabInteractable.selectExited.AddListener(OnWeaponDropped);

            // For full-auto: track trigger held state
            grabInteractable.activated.AddListener(OnTriggerDown);
            grabInteractable.deactivated.AddListener(OnTriggerUp);
        }

        if (magazineSocket != null)
        {
            magazineSocket.selectEntered.AddListener(OnMagazineInserted);
            magazineSocket.selectExited.AddListener(OnMagazineRemoved);
        }

        if (triggerTransform != null)
        {
            triggerOriginalRotation = triggerTransform.localRotation;
        }
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnWeaponGrabbed);
            grabInteractable.selectExited.RemoveListener(OnWeaponDropped);
            grabInteractable.activated.RemoveListener(OnTriggerDown);
            grabInteractable.deactivated.RemoveListener(OnTriggerUp);
        }

        if (magazineSocket != null)
        {
            magazineSocket.selectEntered.RemoveListener(OnMagazineInserted);
            magazineSocket.selectExited.RemoveListener(OnMagazineRemoved);
        }
    }

    private void OnWeaponGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
        // Cache the interactor so we can read its analog trigger value for trigger animation
        currentHoldingInteractor = args.interactorObject as XRBaseInputInteractor;
    }

    private void OnWeaponDropped(SelectExitEventArgs args)
    {
        isHeld = false;
        isTriggerHeld = false;
        currentHoldingInteractor = null;
    }

    private void OnMagazineInserted(SelectEnterEventArgs args)
    {
        Magazine mag = args.interactableObject.transform.GetComponent<Magazine>();
        if (mag != null)
        {
            currentMagazine = mag;
        }
    }

    private void OnMagazineRemoved(SelectExitEventArgs args)
    {
        currentMagazine = null;
    }

    private void OnTriggerDown(ActivateEventArgs args)
    {
        isTriggerHeld = true;

        // For semi-auto, fire immediately on press
        if (!fullAuto)
        {
            FireWeapon();
        }
        else
        {
            // For full-auto, fire the first shot immediately and reset cooldown
            fireCooldownTimer = 0f;
            FireWeapon();
        }
    }

    private void OnTriggerUp(DeactivateEventArgs args)
    {
        isTriggerHeld = false;
    }

    private void Start()
    {
        // Safety check to ensure magazine is registered if it spawned attached to the socket
        if (magazineSocket != null && currentMagazine == null)
        {
            // Specifically checking if there is a starting selected interactable that might have bypassed Awake() events
            UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable attachedObj = magazineSocket.firstInteractableSelected;
            if (attachedObj != null)
            {
                currentMagazine = attachedObj.transform.GetComponent<Magazine>();
                if (currentMagazine != null) Debug.Log("Magazine detected in socket on Start!");
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

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

        if (boltTransform != null)
        {
            originalBoltPosition = boltTransform.localPosition;
        }

        if (shellPrefab != null && shellEjectionPoint != null)
        {
            shellPool = new Queue<GameObject>();
            activeShellCoroutines = new Dictionary<GameObject, Coroutine>();

            // Create a parent object just to keep the hierarchy clean
            Transform poolParent = new GameObject(gameObject.name + "_ShellPool").transform;

            for (int i = 0; i < shellPoolSize; i++)
            {
                GameObject shell = Instantiate(shellPrefab, poolParent);
                shell.SetActive(false);
                shellPool.Enqueue(shell);
            }
        }
    }

    private void Update()
    {
        // ── Full-Auto Firing ──
        if (fullAuto && isTriggerHeld && isHeld)
        {
            fireCooldownTimer -= Time.deltaTime;
            if (fireCooldownTimer <= 0f)
            {
                fireCooldownTimer = 60f / fireRate; // Convert RPM to seconds between shots
                FireWeapon();
            }
        }

        // ── Trigger Animation ──
        if (triggerTransform != null)
        {
            float triggerInput = 0f;
            if (isHeld && currentHoldingInteractor != null)
            {
                // Read the analog trigger value directly from whichever hand is holding the weapon
                triggerInput = currentHoldingInteractor.activateInput.ReadValue();
            }
            // Rotate trigger based on analog input. Positive angle because the asset is backwards.
            triggerTransform.localRotation = triggerOriginalRotation * Quaternion.AngleAxis(triggerInput * triggerMaxAngle, triggerRotateAxis);
        }

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

        if (boltTransform != null)
        {
            if (!isSlideGrabbed)
            {
                targetBoltOffset = Mathf.Lerp(targetBoltOffset, 0f, Time.deltaTime * boltReturnSpeed);
                currentBoltOffset = Mathf.Lerp(currentBoltOffset, targetBoltOffset, Time.deltaTime * boltSnappiness);
            }

            // The prefab is reversed, so a positive local Z offset moves it physically backwards
            boltTransform.localPosition = originalBoltPosition + new Vector3(0, 0, currentBoltOffset);

            // Eject shell when the bolt visibly moves enough (lowered threshold to 30% and made absolute so it triggers reliably)
            if (!hasEjectedShell && Mathf.Abs(currentBoltOffset) > Mathf.Abs(boltTravelDistance) * 0.3f)
            {
                EjectShell();
                hasEjectedShell = true;
            }
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

        if (!isChambered)
        {
            // Dry fire
            if (audioSource != null && dryFireSound != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(dryFireSound, shootVolume);
            }
            return;
        }

        // We have a chambered round, commit to firing!
        isChambered = false; // Spend the chambered round

        // 0. Audio: Play gunshot
        if (audioSource != null && shootSound != null)
        {
            audioSource.pitch = Random.Range(soundPitchRange.x, soundPitchRange.y);
            audioSource.PlayOneShot(shootSound, shootVolume);
        }

        // 0.5: Haptics: Send vibration to controller
        if (grabInteractable != null)
        {
            foreach (var interactor in grabInteractable.interactorsSelecting)
            {
                var inputInteractor = interactor as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor;
                if (inputInteractor != null)
                {
                    inputInteractor.SendHapticImpulse(hapticIntensity, hapticDuration);
                }
            }
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

        if (boltTransform != null)
        {
            targetBoltOffset = boltTravelDistance;
            hasEjectedShell = false; // Prepare a new shell to be ejected as the bolt travels back
        }

        // After firing, the semi-auto gun attempts to chamber a new round
        if (currentMagazine != null && currentMagazine.HasAmmo())
        {
            currentMagazine.ConsumeAmmo();
            isChambered = true;
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

    private void EjectShell()
    {
        if (shellEjectionPoint == null || shellPrefab == null || shellPool == null || shellPool.Count == 0)
        {
            Debug.LogWarning("WeaponController: Cannot eject shell! Ensure 'Shell Prefab' and 'Shell Ejection Point' are assigned in the inspector.");
            return;
        }

        // Dequeue oldest shell (cyclic buffer format)
        GameObject shell = shellPool.Dequeue();

        if (activeShellCoroutines.TryGetValue(shell, out Coroutine existingCoroutine) && existingCoroutine != null)
        {
            StopCoroutine(existingCoroutine);
        }

        shell.SetActive(false);
        shell.transform.position = shellEjectionPoint.position;
        shell.transform.rotation = shellEjectionPoint.rotation;

        Rigidbody shellRb = shell.GetComponent<Rigidbody>();
        if (shellRb != null)
        {
            // Reset velocity physics so it doesn't fly crazy if recycled while moving
            #if UNITY_6000_0_OR_NEWER
            shellRb.linearVelocity = Vector3.zero;
            #else
            shellRb.velocity = Vector3.zero;
            #endif
            shellRb.angularVelocity = Vector3.zero;
        }

        shell.SetActive(true);

        if (shellRb != null)
        {
            // Eject to the right and slightly up, relative to the ejection point
            Vector3 ejectDirection = shellEjectionPoint.right + (shellEjectionPoint.up * 0.3f);
            shellRb.AddForce(ejectDirection.normalized * shellEjectionForce, ForceMode.Impulse);
            
            // Add some random spin
            shellRb.AddTorque(new Vector3(Random.Range(-shellTorque, shellTorque), Random.Range(-shellTorque, shellTorque), Random.Range(-shellTorque, shellTorque)), ForceMode.Impulse);
        }
        
        // Add a slight variance to rotation for visuals
        shell.transform.Rotate(new Vector3(0, Random.Range(-30f, 30f), 0));

        // Start despawn timer for recycling
        Coroutine newCoroutine = StartCoroutine(DisableShellAfterTime(shell, shellLifeTime));
        activeShellCoroutines[shell] = newCoroutine;

        // Requeue to end
        shellPool.Enqueue(shell);
    }

    private IEnumerator DisableShellAfterTime(GameObject shell, float time)
    {
        yield return new WaitForSeconds(time);
        if (shell != null)
        {
            shell.SetActive(false);
        }
    }

    // Editor Helper Method: Allows you to right-click the script in Unity Editor -> "Test Fire Gun"
    // so you can test shooting mechanics without wearing the VR headset!
    [ContextMenu("Test Fire Gun")]
    public void DebugFire()
    {
        // For debug firing, force chamber a round first so it always works
        isChambered = true;
        FireWeapon();
        Debug.Log("Debug Fire Triggered!");
    }

    [ContextMenu("Debug Rack Slide")]
    public void DebugRackSlide()
    {
        RackSlide();
        Debug.Log("Debug Rack Triggered!");
    }

    /// <summary>
    /// Call this from an XR Grab Interactable event or custom script when the slide is fully pulled back.
    /// This mimics a manual rack.
    /// </summary>
    public void RackSlide()
    {
        // 1. If there's an unfired chambered round, eject it. 
        if (isChambered)
        {
            EjectShell();
            isChambered = false;
        }

        // 2. Chamber a new round from the magazine if available
        if (currentMagazine != null)
        {
            if (currentMagazine.HasAmmo())
            {
                currentMagazine.ConsumeAmmo();
                isChambered = true;
                Debug.Log("Round Chambered! Ammo left in mag: " + currentMagazine.currentAmmo);
            }
            else
            {
                Debug.LogWarning("Magazine is inserted, but it is empty!");
            }
        }
        else
        {
            Debug.LogWarning("Could not chamber round: No Magazine detected in the Socket!");
        }

        // Optionally visually rack the bolt via the procedural animation back to simulate the rack action
        if (boltTransform != null)
        {
            targetBoltOffset = boltTravelDistance;
            currentBoltOffset = boltTravelDistance; // Snap it back instantly so you can see it return
            // We set this to true so we don't accidentally double-eject a shell through the Update logic
            hasEjectedShell = true;
        }
    }
}
