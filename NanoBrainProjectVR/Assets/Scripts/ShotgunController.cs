using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.Netcode;

[RequireComponent(typeof(TwoHandGrabInteractable))]
public class ShotgunController : NetworkBehaviour
{
    public enum ChamberState { Empty, SpentShell, LiveRound }

    [Header("Shotgun Internals")]
    [Tooltip("How many shells the internal tube can hold")]
    public int maxAmmoCapacity = 8;
    [Tooltip("Initial ammo in the tube when spawned")]
    public int initialAmmo = 6;
    public NetworkVariable<int> currentAmmo = new NetworkVariable<int>(6, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [Tooltip("How many rounds are loaded when 1 physical shell is inserted (QoL)")]
    public int ammoPerShellReloaded = 3;
    
    public NetworkVariable<ChamberState> chamberState = new NetworkVariable<ChamberState>(ChamberState.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Pellet Spread")]
    public float damagePerPellet = 34f;
    [Tooltip("How far (in meters) the pellet can penetrate into targets before stopping.")]
    public float penetrationDepth = 0.5f;
    public int pelletCount = 8;

    // Events for Training Grounds
    public static event System.Action<int> OnProjectilesFired;
    [Tooltip("The cone angle in degrees for the spread")]
    public float spreadAngle = 4f;
    public float range = 50f;
    public LayerMask hitMask = ~0; 
    public Transform barrelPoint;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip shootSound;
    public AudioClip dryFireSound;
    public AudioClip pumpBackSound;
    public AudioClip pumpForwardSound;
    public Vector2 soundPitchRange = new Vector2(0.95f, 1.05f);
    [Range(0f, 1f)] public float shootVolume = 1f;

    [Header("Controller Haptics")]
    [Range(0f, 1f)] public float hapticIntensity = 0.8f;
    public float hapticDuration = 0.15f;

    [Header("Shell Ejection")]
    public GameObject shellPrefab;
    public Transform shellEjectionPoint;
    public float shellEjectionForce = 3f;
    public float shellTorque = 1f;
    public int shellPoolSize = 10;
    public float shellLifeTime = 4f;
    private Queue<GameObject> shellPool;
    private Dictionary<GameObject, Coroutine> activeShellCoroutines;

    [Header("Interactables Group")]
    [Tooltip("Assign the parent GameObject that holds all sub-interactables (Pump, Loading Port). It will be disabled when the weapon is dropped to prevent accidental grabs.")]
    public GameObject subInteractablesGroup;

    [Header("Procedural Recoil")]
    public Transform weaponModel;
    public Vector2 pitchRange = new Vector2(5.0f, 8.0f);
    public Vector2 yawRange = new Vector2(-2.0f, 2.0f);
    public float backwardKick = 0.06f;
    public float snappiness = 25f;
    public float returnSpeed = 5f;
    public float twoHandRecoilModifier = 0.5f;

    private Vector3 originalModelRotation;
    private Vector3 originalModelPosition;
    private Vector3 currentRotation;
    private Vector3 targetRotation;
    private Vector3 currentPosition;
    private Vector3 targetPosition;

    [Header("Smart Ammo Pouch")]
    public GameObject magazinePrefab;

    [Header("Visual Effects")]
    public GameObject muzzleFlash;
    private ParticleSystem[] cachedMuzzleFlashParticles;

    [Header("Trigger Animation")]
    [Tooltip("The trigger bone/transform on the weapon model")]
    public Transform triggerTransform;
    [Tooltip("Maximum rotation angle when the trigger is fully pulled (degrees). Positive = rotates on local X since the asset is backwards.")]
    public float triggerMaxAngle = 15f;
    [Tooltip("The axis around which the trigger rotates locally")]
    public Vector3 triggerRotateAxis = Vector3.right;
    private Quaternion triggerOriginalRotation;
    private bool isHeld = false;

    private TwoHandGrabInteractable grabInteractable;
    private XRBaseInputInteractor currentHoldingInteractor;

    private void Awake()
    {
        SetSubInteractablesState(false);

        grabInteractable = GetComponent<TwoHandGrabInteractable>();
        if (grabInteractable != null)
        {
            // VERY IMPORTANT FOR MULTIPLAYER!
            // If XR Interaction Toolkit reparents a NetworkObject to a hand or socket locally, Unity Netcode panics and despawns it for all other players!
            // This forces XRI to leave the weapon in the root of the scene and only move its position/rotation.
            grabInteractable.retainTransformParent = true;
        }

        if (weaponModel != null)
        {
            originalModelRotation = weaponModel.localEulerAngles;
            originalModelPosition = weaponModel.localPosition;
        }

        if (triggerTransform != null)
        {
            triggerOriginalRotation = triggerTransform.localRotation;
        }
    }

    private void SetSubInteractablesState(bool state)
    {
        if (subInteractablesGroup != null)
        {
            var interactables = subInteractablesGroup.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true);
            foreach (var interactable in interactables)
            {
                if (interactable == grabInteractable) continue;
                interactable.enabled = state;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer || IsOwner)
        {
            if (currentAmmo.Value == 0 && initialAmmo > 0)
            {
                currentAmmo.Value = initialAmmo;
            }
            if (initialAmmo > 0)
            {
                chamberState.Value = ChamberState.LiveRound;
            }
            else
            {
                chamberState.Value = ChamberState.Empty;
            }
        }
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (currentAmmo.Value == 0 && initialAmmo > 0)
            {
                currentAmmo.Value = initialAmmo;
            }
            if (initialAmmo > 0)
            {
                chamberState.Value = ChamberState.LiveRound;
            }
            else
            {
                chamberState.Value = ChamberState.Empty;
            }
        }

        if (shellPrefab != null && shellEjectionPoint != null)
        {
            shellPool = new Queue<GameObject>();
            activeShellCoroutines = new Dictionary<GameObject, Coroutine>();
            GameObject shellParent = new GameObject("ShotgunShellPool");
            shellParent.transform.SetParent(transform, false);

            for (int i = 0; i < shellPoolSize; i++)
            {
                GameObject shell = Instantiate(shellPrefab, shellParent.transform);
                shell.SetActive(false);
                shellPool.Enqueue(shell);
            }
        }

        if (muzzleFlash != null)
        {
            cachedMuzzleFlashParticles = muzzleFlash.GetComponentsInChildren<ParticleSystem>(true);
        }

        if (shootSound != null) shootSound.LoadAudioData();
        if (dryFireSound != null) dryFireSound.LoadAudioData();

        StartCoroutine(GPUWarmupRoutine());
    }

    private System.Collections.IEnumerator GPUWarmupRoutine()
    {
        yield return null;
        yield return null;

        if (muzzleFlash != null)
        {
            Vector3 originalScale = muzzleFlash.transform.localScale;
            muzzleFlash.transform.localScale = Vector3.one * 0.0001f;
            muzzleFlash.SetActive(true);

            if (cachedMuzzleFlashParticles != null)
            {
                foreach (var p in cachedMuzzleFlashParticles)
                {
                    p.Play(true);
                }
            }

            yield return null;

            if (cachedMuzzleFlashParticles != null)
            {
                foreach (var p in cachedMuzzleFlashParticles)
                {
                    p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            muzzleFlash.SetActive(false);
            muzzleFlash.transform.localScale = originalScale;
        }

        if (shellPool != null && shellPool.Count > 0)
        {
            GameObject shell = shellPool.Peek();
            if (shell != null)
            {
                Vector3 origScale = shell.transform.localScale;
                shell.transform.localScale = Vector3.one * 0.0001f;
                shell.SetActive(true);
                yield return null;
                shell.SetActive(false);
                shell.transform.localScale = origScale;
            }
        }

        if (audioSource != null && shootSound != null)
        {
            float oldVol = audioSource.volume;
            audioSource.volume = 0f;
            audioSource.PlayOneShot(shootSound);
            yield return null;
            audioSource.Stop();
            audioSource.volume = oldVol;
        }
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnWeaponGrabbed);
            grabInteractable.selectExited.AddListener(OnWeaponReleased);
            grabInteractable.activated.AddListener(OnTriggerPulled);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnWeaponGrabbed);
            grabInteractable.selectExited.RemoveListener(OnWeaponReleased);
            grabInteractable.activated.RemoveListener(OnTriggerPulled);
        }
    }

    private void OnWeaponGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
        
        IXRSelectInteractor interactor = args.interactorObject;
        // Only activate sub-interactables if grabbed by a HAND (Direct/Ray interactor), NOT a Socket!
        if (!(interactor is XRSocketInteractor))
        {
            SetSubInteractablesState(true);

            // FIX: If the weapon's Select Mode is "Multiple", the socket will try to share the weapon with the hand instead of letting it go!
            var interactorsSelecting = grabInteractable.interactorsSelecting;
            for (int i = interactorsSelecting.Count - 1; i >= 0; i--)
            {
                if (interactorsSelecting[i] is XRSocketInteractor socket)
                {
                    StartCoroutine(ForceSocketReleaseRoutine(socket, args.manager));
                }
            }

            // GUARANTEE PHYSICS ARE ACTIVE:
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }

            // --- NEW: Smart Ammo Pouch Logic (Multiplayer Safe) ---
            // Find the exact Ammo Pouch attached to the specific player who grabbed the gun
            if (!IsSpawned || IsOwner)
            {
                if (magazinePrefab != null)
                {
                    AmmoPouch localPouch = args.interactorObject.transform.root.GetComponentInChildren<AmmoPouch>();

                    if (localPouch != null)
                    {
                        localPouch.SetMagazinePrefab(magazinePrefab);
                    }
                    else
                    {
                        Debug.LogWarning($"<color=yellow>[ShotgunController]</color> {gameObject.name} grabbed, but no AmmoPouch was found on the player's rig ({args.interactorObject.transform.root.name})!");
                    }
                }
                else
                {
                    Debug.LogWarning($"<color=red>[ShotgunController]</color> {gameObject.name} grabbed, but its Magazine Prefab is MISSING in the Inspector!");
                }
            }

            // Explicit Network Un-Parenting is now handled strictly by WeaponAutoReturn.cs OnGrabbed!
        }

        currentHoldingInteractor = args.interactorObject as XRBaseInputInteractor;
    }

    private System.Collections.IEnumerator ForceSocketReleaseRoutine(XRSocketInteractor socket, UnityEngine.XR.Interaction.Toolkit.XRInteractionManager manager)
    {
        // Wait 1 frame to completely escape the Interaction Manager's event lock!
        yield return new WaitForEndOfFrame();
        
        if (socket != null && grabInteractable != null)
        {
            // Explicitly force the socket to drop the weapon now that the event loop is safe
            manager.SelectCancel((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)socket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
            
            // Turn the socket completely off so it doesn't instantly snatch the weapon right back!
            socket.socketActive = false;
        }

        // Wait for 1.5 seconds so the player has time to pull the weapon away
        yield return new WaitForSeconds(1.5f);
        
        // Turn it back on so it can catch weapons again
        if (socket != null)
        {
            socket.socketActive = true;
        }
    }

    private void OnWeaponReleased(SelectExitEventArgs args)
    {
        isHeld = false;

        SetSubInteractablesState(false);

        currentHoldingInteractor = null;

        // Ensure physics are completely restored so the shotgun falls to the ground when dropped!
        bool droppedIntoSocket = args.interactorObject is XRSocketInteractor;
        if (!droppedIntoSocket)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        // --- NEW: Smart Ammo Pouch Logic for Dropping ---
        AmmoPouch localPouch = args.interactorObject.transform.root.GetComponentInChildren<AmmoPouch>();
        if (localPouch != null)
        {
            var interactors = args.interactorObject.transform.root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>();
            foreach (var interactor in interactors)
            {
                if (interactor.hasSelection)
                {
                    var grabbedObj = interactor.interactablesSelected[0].transform.gameObject;
                    
                    WeaponController wc = grabbedObj.GetComponent<WeaponController>();
                    if (wc != null && wc.gameObject != this.gameObject && wc.magazinePrefab != null)
                    {
                        localPouch.SetMagazinePrefab(wc.magazinePrefab);
                        break;
                    }
                    
                    ShotgunController sc = grabbedObj.GetComponent<ShotgunController>();
                    if (sc != null && sc != this && sc.magazinePrefab != null)
                    {
                        localPouch.SetMagazinePrefab(sc.magazinePrefab);
                        break;
                    }
                }
            }
        }
    }

    private void OnTriggerPulled(ActivateEventArgs args)
    {
        if (IsSpawned && !IsOwner) return;
        
        FireWeaponLocal();
    }

    private void FireWeaponLocal()
    {
        if (chamberState.Value != ChamberState.LiveRound)
        {
            PlayDryFireLocal();
            if (IsSpawned) PlayDryFireRpc();
            return;
        }

        chamberState.Value = ChamberState.SpentShell;

        // Report to Training Grounds Manager
        OnProjectilesFired?.Invoke(pelletCount);

        // Pre-allocate buffer for RaycastNonAlloc to avoid GC spikes
        RaycastHit[] hitBuffer = new RaycastHit[20];

        // Perform Raycasts on the server for authority (or locally if offline)
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 spreadDirection = barrelPoint.forward;
            
            // Random cone spread
            Vector3 randomPoint = Random.insideUnitSphere;
            spreadDirection = Quaternion.AngleAxis(Random.Range(0f, spreadAngle), randomPoint) * spreadDirection;

            int hitCount = Physics.RaycastNonAlloc(barrelPoint.position, spreadDirection, hitBuffer, range, hitMask, QueryTriggerInteraction.Collide);

            RaycastHit closestSolidHit = new RaycastHit();
            float closestSolidDist = float.MaxValue;
            HittableSurface closestHittable = null;

            // Track damage zones so we apply damage AFTER placing the decal
            System.Collections.Generic.List<TargetDamageZone> zonesToDamage = new System.Collections.Generic.List<TargetDamageZone>();
            System.Collections.Generic.List<AnimalDamageZone> animalZonesToDamage = new System.Collections.Generic.List<AnimalDamageZone>();

            // Sort hits by distance to correctly calculate pellet penetration
            for (int k = 0; k < hitCount - 1; k++) {
                for (int m = k + 1; m < hitCount; m++) {
                    if (hitBuffer[k].distance > hitBuffer[m].distance) {
                        RaycastHit temp = hitBuffer[k];
                        hitBuffer[k] = hitBuffer[m];
                        hitBuffer[m] = temp;
                    }
                }
            }

            float entryDistance = -1f;

            for (int j = 0; j < hitCount; j++)
            {
                RaycastHit currentHit = hitBuffer[j];

                // 1. Mark our entry point into a solid object (e.g. skin, tree, wall)
                if (entryDistance < 0f && !currentHit.collider.isTrigger)
                {
                    entryDistance = currentHit.distance;
                }

                // 2. Check if the pellet ran out of penetration power
                if (entryDistance >= 0f && (currentHit.distance - entryDistance) > penetrationDepth)
                {
                    // The pellet stopped inside the object!
                    break;
                }

                // 1a. Check for Standard Target Damage Zones
                TargetDamageZone damageZone = currentHit.collider.GetComponentInParent<TargetDamageZone>();
                if (damageZone != null)
                {
                    if (!zonesToDamage.Contains(damageZone))
                        zonesToDamage.Add(damageZone);
                }

                // 1b. Check for Animal Damage Zones
                AnimalDamageZone animalZone = currentHit.collider.GetComponentInParent<AnimalDamageZone>();
                if (animalZone != null)
                {
                    if (!animalZonesToDamage.Contains(animalZone))
                        animalZonesToDamage.Add(animalZone);
                }

                // 2. Check for solid surface for Decal
                if (!currentHit.collider.isTrigger)
                {
                    HittableSurface hittable = currentHit.collider.GetComponentInParent<HittableSurface>();
                    if (hittable != null && currentHit.distance < closestSolidDist)
                    {
                        closestSolidDist = currentHit.distance;
                        closestSolidHit = currentHit;
                        closestHittable = hittable;
                    }
                }
            }

            if (closestHittable != null)
            {
                SurfaceType hitType = closestHittable.surfaceType;
                closestHittable.OnHit(closestSolidHit);
                
                SpawnDecalLocal(closestSolidHit.point, closestSolidHit.normal, hitType, closestSolidHit.collider.transform);
                if (IsSpawned) SpawnDecalRpc(closestSolidHit.point, closestSolidHit.normal, hitType);
            }

            // 4. NOW apply the damage (so the target doesn't rotate BEFORE the decal is parented!)
            foreach (var zone in zonesToDamage)
            {
                zone.ApplyDamage(damagePerPellet);
            }

            foreach (var animalZone in animalZonesToDamage)
            {
                animalZone.ApplyDamage(damagePerPellet);
            }
        }

        PlayShootEffectsLocal();
        if (IsSpawned) PlayShootEffectsRpc();
    }

    [Rpc(SendTo.NotOwner)]
    private void SpawnDecalRpc(Vector3 point, Vector3 normal, SurfaceType type)
    {
        SpawnDecalLocal(point, normal, type, null);
    }

    private void SpawnDecalLocal(Vector3 point, Vector3 normal, SurfaceType type, Transform parentTransform)
    {
        if (HitEffectPoolManager.Instance != null)
        {
            HitEffectPoolManager.Instance.SpawnHitEffect(point, normal, type, parentTransform);
        }
    }

    [Rpc(SendTo.NotOwner)]
    private void PlayDryFireRpc()
    {
        PlayDryFireLocal();
    }

    private void PlayDryFireLocal()
    {
        if (audioSource != null && dryFireSound != null)
            audioSource.PlayOneShot(dryFireSound, shootVolume);
    }

    [Rpc(SendTo.NotOwner)]
    private void PlayShootEffectsRpc()
    {
        PlayShootEffectsLocal();
    }

    private void PlayShootEffectsLocal()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.SetActive(false);
            muzzleFlash.SetActive(true);
            if (cachedMuzzleFlashParticles != null)
            {
                foreach (ParticleSystem p in cachedMuzzleFlashParticles)
                {
                    p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    p.Play(true);
                }
            }
        }
        
        if (audioSource != null && shootSound != null)
        {
            audioSource.pitch = Random.Range(soundPitchRange.x, soundPitchRange.y);
            audioSource.PlayOneShot(shootSound, shootVolume);
        }

        if (currentHoldingInteractor != null && currentHoldingInteractor is XRBaseInputInteractor ctrl)
        {
            ctrl.SendHapticImpulse(hapticIntensity, hapticDuration);
        }

        ApplyProceduralRecoil();
    }

    // ────────────────────────────────────────────────────────────────────────
    // PUMP ACTION LOGIC
    // ────────────────────────────────────────────────────────────────────────

    // Call this via UnityEvent on the TwoHandGrabInteractable!
    public void OnPumpPulledBack()
    {
        if (IsSpawned && !IsOwner) return;

        if (chamberState.Value == ChamberState.SpentShell || chamberState.Value == ChamberState.LiveRound)
        {
            EjectShellLocal();
            if (IsSpawned) EjectShellRpc();
        }

        chamberState.Value = ChamberState.Empty;
        
        PlaySoundLocal(true);
        if (IsSpawned) PlaySoundRpc(true);
    }

    // Call this via UnityEvent on the TwoHandGrabInteractable!
    public void OnPumpPushedForward()
    {
        if (IsSpawned && !IsOwner) return;

        if (chamberState.Value == ChamberState.Empty && currentAmmo.Value > 0)
        {
            currentAmmo.Value--;
            chamberState.Value = ChamberState.LiveRound;
        }

        PlaySoundLocal(false);
        if (IsSpawned) PlaySoundRpc(false);
    }

    [Rpc(SendTo.NotOwner)]
    private void EjectShellRpc()
    {
        EjectShellLocal();
    }

    private void EjectShellLocal()
    {
        if (shellEjectionPoint == null || shellPrefab == null || shellPool == null || shellPool.Count == 0) return;

        GameObject shell = shellPool.Dequeue();
        
        // Failsafe: if a shell in the pool was destroyed by a scene unload, instantiate a fresh one!
        if (shell == null)
        {
            Transform poolParent = transform.Find("ShotgunShellPool");
            if (poolParent == null)
            {
                poolParent = new GameObject("ShotgunShellPool").transform;
                poolParent.SetParent(transform, false);
            }
            shell = Instantiate(shellPrefab, poolParent);
        }

        // Move the shell to the ejection port BEFORE applying physics!
        shell.transform.position = shellEjectionPoint.position;
        shell.transform.rotation = shellEjectionPoint.rotation;
        
        shell.SetActive(true);

        Rigidbody shellRb = shell.GetComponent<Rigidbody>();
        if (shellRb != null)
        {
            #if UNITY_6000_0_OR_NEWER
            shellRb.linearVelocity = Vector3.zero;
            #else
            shellRb.velocity = Vector3.zero;
            #endif
            shellRb.angularVelocity = Vector3.zero;

            Vector3 ejectDirection = shellEjectionPoint.right + (shellEjectionPoint.up * Random.Range(0.2f, 0.5f));
            shellRb.AddForce(ejectDirection.normalized * shellEjectionForce, ForceMode.Impulse);
            shellRb.AddTorque(new Vector3(Random.Range(-shellTorque, shellTorque), Random.Range(-shellTorque, shellTorque), Random.Range(-shellTorque, shellTorque)), ForceMode.Impulse);
        }

        if (activeShellCoroutines.TryGetValue(shell, out Coroutine existing))
        {
            if (existing != null) StopCoroutine(existing);
            activeShellCoroutines.Remove(shell);
        }

        activeShellCoroutines.Add(shell, StartCoroutine(ReturnShellToPool(shell, shellLifeTime)));
        shellPool.Enqueue(shell);
    }

    private IEnumerator ReturnShellToPool(GameObject shell, float time)
    {
        yield return new WaitForSeconds(time);
        if (shell != null) shell.SetActive(false);
    }

    [Rpc(SendTo.NotOwner)]
    private void PlaySoundRpc(bool isPullBack)
    {
        PlaySoundLocal(isPullBack);
    }

    private void PlaySoundLocal(bool isPullBack)
    {
        if (audioSource == null) return;

        if (isPullBack && pumpBackSound != null)
            audioSource.PlayOneShot(pumpBackSound, shootVolume);
        else if (!isPullBack && pumpForwardSound != null)
            audioSource.PlayOneShot(pumpForwardSound, shootVolume);
    }

    // ────────────────────────────────────────────────────────────────────────
    // RECOIL LOGIC
    // ────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (weaponModel != null)
        {
            targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, Time.deltaTime * returnSpeed);
            targetPosition = Vector3.Lerp(targetPosition, Vector3.zero, Time.deltaTime * returnSpeed);

            currentRotation = Vector3.Slerp(currentRotation, targetRotation, Time.deltaTime * snappiness);
            currentPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * snappiness);

            weaponModel.localRotation = Quaternion.Euler(originalModelRotation + currentRotation);
            weaponModel.localPosition = originalModelPosition + currentPosition;
        }

        // Check for F key using the New Input System
        if ((!IsSpawned || IsOwner) && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                DebugPumpAction();
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
    }

    public void DebugPumpAction()
    {
        OnPumpPulledBack();
        StartCoroutine(DebugPumpForwardDelay());
    }

    private IEnumerator DebugPumpForwardDelay()
    {
        yield return new WaitForSeconds(0.15f);
        OnPumpPushedForward();
    }

    private void ApplyProceduralRecoil()
    {
        if (weaponModel != null)
        {
            float pitch = Random.Range(pitchRange.x, pitchRange.y);
            float yaw = Random.Range(yawRange.x, yawRange.y);
            float kick = backwardKick;

            if (grabInteractable != null && grabInteractable.IsTwoHandedGrabbed)
            {
                pitch *= twoHandRecoilModifier;
                yaw *= twoHandRecoilModifier;
                kick *= twoHandRecoilModifier;
            }

            targetRotation += new Vector3(-pitch, yaw, 0);
            targetPosition -= new Vector3(0, 0, kick);
        }
    }
}
