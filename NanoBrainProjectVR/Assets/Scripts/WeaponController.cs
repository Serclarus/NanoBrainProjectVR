using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit; // Required for XR Grab Interactable
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Required for XRSocketInteractor
using Unity.Netcode; // Required for NGO

[RequireComponent(typeof(TwoHandGrabInteractable))]
public class WeaponController : NetworkBehaviour
{
    [Header("Weapon Profile")]
    [Tooltip("The ScriptableObject containing all the combat stats (damage, fire rate, recoil, etc.)")]
    public WeaponDataSO weaponData;

    private TwoHandGrabInteractable grabInteractable;

    [Header("Shooting Setup")]
    [Tooltip("Which layers should the bullet hit?")]
    public LayerMask hitMask = ~0; 
    [Tooltip("The point where the raycast bullet originates")]
    public Transform barrelPoint;

    [Header("Fire Mode")]
    private float fireCooldownTimer = 0f;
    private bool isTriggerHeld = false;
    private bool isHeld = false;
    private bool hasPlayedDryFire = false;

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
    public NetworkVariable<bool> isChambered = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Visual Effects")]
    [Tooltip("Assign the generic muzzle flash particle system here")]
    public ParticleSystem muzzleFlash; 

    [Header("Audio Settings")]
    [Tooltip("The AudioSource component used to play the sound")]
    public AudioSource audioSource;

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
    [Tooltip("How many shells to keep in the pool")]
    public int shellPoolSize = 15;
    [Tooltip("How long a shell lives before being disabled")]
    public float shellLifeTime = 4f;

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

    private int consecutiveShots = 0;
    
    private Vector3 originalModelRotation;
    private Vector3 originalModelPosition;

    // Variables for procedural spring math
    private Vector3 currentRotation;
    private Vector3 targetRotation;
    private Vector3 currentPosition;
    private Vector3 targetPosition;

    private void Awake()
    {
        grabInteractable = GetComponent<TwoHandGrabInteractable>();
        
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

    public override void OnDestroy()
    {
        base.OnDestroy();
        
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
        hasPlayedDryFire = false;

        if (weaponData == null) return;

        // For semi-auto, fire immediately on press
        if (!weaponData.fullAuto)
        {
            FireWeapon();
        }
        else
        {
            // For full-auto, fire the first shot immediately and start cooldown
            fireCooldownTimer = 60f / weaponData.fireRate;
            FireWeapon();
        }
    }

    private void OnTriggerUp(DeactivateEventArgs args)
    {
        isTriggerHeld = false;
        consecutiveShots = 0; // Reset burst counter when trigger is released
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

        if (weaponData != null && weaponData.shellPrefab != null && shellEjectionPoint != null)
        {
            shellPool = new Queue<GameObject>();
            activeShellCoroutines = new Dictionary<GameObject, Coroutine>();

            // Create a parent object just to keep the hierarchy clean
            Transform poolParent = new GameObject(gameObject.name + "_ShellPool").transform;

            for (int i = 0; i < shellPoolSize; i++)
            {
                GameObject shell = Instantiate(weaponData.shellPrefab, poolParent);
                shell.SetActive(false);
                shellPool.Enqueue(shell);
            }
        }
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
        {
            DebugRackSlide();
        }
#endif

        // ── Full-Auto Firing ──
        if (weaponData != null && weaponData.fullAuto && isTriggerHeld && isHeld)
        {
            fireCooldownTimer -= Time.deltaTime;
            if (fireCooldownTimer <= 0f)
            {
                fireCooldownTimer = 60f / weaponData.fireRate; // Convert RPM to seconds between shots
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
            targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, Time.deltaTime * (weaponData != null ? weaponData.returnSpeed : 8f));
            targetPosition = Vector3.Lerp(targetPosition, Vector3.zero, Time.deltaTime * (weaponData != null ? weaponData.returnSpeed : 8f));

            // 2. The current visible position snaps sharply towards the targets
            currentRotation = Vector3.Slerp(currentRotation, targetRotation, Time.deltaTime * (weaponData != null ? weaponData.snappiness : 20f));
            currentPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * (weaponData != null ? weaponData.snappiness : 20f));

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

            // For a correct prefab (+Z forward), moving backwards requires a negative local Z offset
            boltTransform.localPosition = originalBoltPosition + new Vector3(0, 0, -currentBoltOffset);

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
        if (weaponData == null)
        {
            Debug.LogWarning("WeaponController: No WeaponDataSO assigned! Cannot fire.");
            return;
        }

        if (IsSpawned && !IsOwner) return; // Only owner calculates hits and ammo

        if (ShootingRangeManager.Instance != null && !ShootingRangeManager.Instance.isShootingAllowed)
        {
            if (!hasPlayedDryFire)
            {
                if (IsSpawned) DryFireRpc();
                else PlayDryFireLocal();
                
                hasPlayedDryFire = true;
            }
            return;
        }

        if (barrelPoint == null)
        {
            Debug.LogWarning("WeaponController: No barrel point assigned! Please create an empty GameObject at the barrel tip and assign it.");
            return;
        }

        if (!isChambered.Value)
        {
            // Dry fire
            if (!hasPlayedDryFire)
            {
                if (IsSpawned) DryFireRpc();
                else PlayDryFireLocal();
                
                hasPlayedDryFire = true;
            }
            return;
        }

        // We have a chambered round, commit to firing!
        isChambered.Value = false; // Spend the chambered round
        consecutiveShots++;

        // After firing, the semi-auto gun attempts to chamber a new round
        if (currentMagazine != null && currentMagazine.HasAmmo())
        {
            currentMagazine.ConsumeAmmo();
            isChambered.Value = true;
        }

        // 3. Logic: Raycast Hit Detection
        bool hitSomething = false;
        Vector3 hitPoint = Vector3.zero;
        Vector3 hitNormal = Vector3.zero;
        SurfaceType hitType = SurfaceType.Default;

        if (Physics.Raycast(barrelPoint.position, barrelPoint.forward, out RaycastHit hit, weaponData.range, hitMask))
        {
            hitSomething = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;

            // 4. Hit Logic (Highly Optimized: Single GetComponent call)
            HittableSurface hittable = hit.collider.GetComponentInParent<HittableSurface>();
            
            if (hittable != null)
            {
                // Read the material type for the particles
                hitType = hittable.surfaceType;
                
                // Trigger the damage/score logic
                hittable.OnHit(hit);
            }

            // Spawn hit effect from the pool precisely on the surface locally with parent
            if (HitEffectPoolManager.Instance != null)
            {
                HitEffectPoolManager.Instance.SpawnHitEffect(hit.point, hit.normal, hitType, hit.collider.transform);
            }
        }

        // Tell all clients to play visuals
        if (IsSpawned)
            FireVisualsRpc(consecutiveShots);
        else
            PlayFireVisualsLocal(consecutiveShots);
        
        if (hitSomething)
        {
            if (IsSpawned)
                SpawnHitEffectRpc(hitPoint, hitNormal, hitType);
            else
                SpawnHitEffectLocal(hitPoint, hitNormal, hitType);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void DryFireRpc()
    {
        PlayDryFireLocal();
    }

    private void PlayDryFireLocal()
    {
        if (audioSource != null && weaponData != null && weaponData.dryFireSound != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(weaponData.dryFireSound, weaponData.shootVolume);
        }
    }

    [Rpc(SendTo.NotOwner)]
    private void SpawnHitEffectRpc(Vector3 hitPoint, Vector3 hitNormal, SurfaceType hitType)
    {
        SpawnHitEffectLocal(hitPoint, hitNormal, hitType);
    }

    private void SpawnHitEffectLocal(Vector3 hitPoint, Vector3 hitNormal, SurfaceType hitType)
    {
        if (HitEffectPoolManager.Instance != null)
        {
            // Spawn without parent on non-owner/offline clients
            HitEffectPoolManager.Instance.SpawnHitEffect(hitPoint, hitNormal, hitType, null);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void FireVisualsRpc(int shots)
    {
        PlayFireVisualsLocal(shots);
    }

    private void PlayFireVisualsLocal(int shots)
    {
        // 0. Audio: Play gunshot
        if (audioSource != null && weaponData != null && weaponData.shootSound != null)
        {
            audioSource.pitch = Random.Range(weaponData.soundPitchRange.x, weaponData.soundPitchRange.y);
            audioSource.PlayOneShot(weaponData.shootSound, weaponData.shootVolume);
        }

        // 0.5: Haptics: Send vibration to controller
        if (isHeld && grabInteractable != null)
        {
            foreach (var interactor in grabInteractable.interactorsSelecting)
            {
                var inputInteractor = interactor as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor;
                if (inputInteractor != null && weaponData != null)
                {
                    inputInteractor.SendHapticImpulse(weaponData.hapticIntensity, weaponData.hapticDuration);
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

        // 2. Procedural Recoil Integration: pick the correct tier based on consecutive shot count
        if (weaponModel != null && weaponData != null)
        {
            // Select the recoil tier based on how many consecutive shots have been fired
            RecoilTier tier;
            if (shots <= 3)
                tier = weaponData.tier1_Shots1to3;
            else if (shots <= 8)
                tier = weaponData.tier2_Shots4to8;
            else if (shots <= 17)
                tier = weaponData.tier3_Shots9to17;
            else
                tier = weaponData.tier4_Shots18plus;

            float pitch = Random.Range(tier.pitchRange.x, tier.pitchRange.y);
            float yaw = Random.Range(tier.yawRange.x, tier.yawRange.y);
            float roll = Random.Range(tier.rollRange.x, tier.rollRange.y);
            float kick = tier.backwardKick;

            if (grabInteractable != null && grabInteractable.IsTwoHandedGrabbed)
            {
                pitch *= weaponData.twoHandRecoilModifier;
                yaw *= weaponData.twoHandRecoilModifier;
                roll *= weaponData.twoHandRecoilModifier;
                kick *= weaponData.twoHandRecoilModifier;
            }

            // Add onto the current recoil target for stacking
            // -pitch pitches UP on a standard Unity object (+Z forward)
            targetRotation += new Vector3(-pitch, yaw, roll);
            targetPosition += new Vector3(0, 0, -kick);
        }

        if (boltTransform != null)
        {
            targetBoltOffset = boltTravelDistance;
            hasEjectedShell = false; // Prepare a new shell to be ejected as the bolt travels back
        }
    }

    private void EjectShell()
    {
        if (weaponData == null || shellEjectionPoint == null || weaponData.shellPrefab == null || shellPool == null || shellPool.Count == 0)
        {
            Debug.LogWarning("WeaponController: Cannot eject shell! Ensure 'WeaponData' and 'Shell Ejection Point' are assigned.");
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
            shellRb.AddForce(ejectDirection.normalized * weaponData.shellEjectionForce, ForceMode.Impulse);
            
            // Add some random spin
            shellRb.AddTorque(new Vector3(Random.Range(-weaponData.shellTorque, weaponData.shellTorque), Random.Range(-weaponData.shellTorque, weaponData.shellTorque), Random.Range(-weaponData.shellTorque, weaponData.shellTorque)), ForceMode.Impulse);
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
        isChambered.Value = true;
        FireWeapon();
        Debug.Log("Debug Fire Triggered!");
    }

    [ContextMenu("Debug Rack Slide")]
    public void DebugRackSlide()
    {
        RackSlide();
        Debug.Log("Debug Rack Triggered!");
    }

    public void RackSlide()
    {
        if (IsSpawned && !IsOwner) return; // Only owner initiates mechanical actions
        
        bool ejectRound = false;
        
        // 1. If there's an unfired chambered round, eject it. 
        if (isChambered.Value)
        {
            ejectRound = true;
            isChambered.Value = false;
        }

        // 2. Chamber a new round from the magazine if available
        if (currentMagazine != null)
        {
            if (currentMagazine.HasAmmo())
            {
                currentMagazine.ConsumeAmmo();
                isChambered.Value = true;
                Debug.Log("Round Chambered!");
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

        if (IsSpawned)
            RackSlideRpc(ejectRound);
        else
            PlayRackSlideLocal(ejectRound);
    }

    [Rpc(SendTo.Everyone)]
    private void RackSlideRpc(bool ejectRound)
    {
        PlayRackSlideLocal(ejectRound);
    }

    private void PlayRackSlideLocal(bool ejectRound)
    {
        if (ejectRound)
        {
            EjectShell();
        }

        // Visually rack the bolt via the procedural animation back to simulate the rack action
        if (boltTransform != null)
        {
            targetBoltOffset = boltTravelDistance;
            currentBoltOffset = boltTravelDistance; // Snap it back instantly so you can see it return
            // We set this to true so we don't accidentally double-eject a shell through the Update logic
            hasEjectedShell = true;
        }
    }
}
