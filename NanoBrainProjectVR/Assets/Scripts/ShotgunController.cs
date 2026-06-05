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
    public NetworkVariable<int> currentAmmo = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    [Tooltip("How many rounds are loaded when 1 physical shell is inserted (QoL)")]
    public int ammoPerShellReloaded = 3;
    
    public NetworkVariable<ChamberState> chamberState = new NetworkVariable<ChamberState>(ChamberState.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Pellet Spread")]
    public float damagePerPellet = 5f;
    public int pelletCount = 8;
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
    public ParticleSystem muzzleFlash;

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
        grabInteractable = GetComponent<TwoHandGrabInteractable>();
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

    private void Start()
    {
        if (shellPrefab != null && shellEjectionPoint != null)
        {
            shellPool = new Queue<GameObject>();
            activeShellCoroutines = new Dictionary<GameObject, Coroutine>();
            GameObject shellParent = new GameObject("ShotgunShellPool");

            for (int i = 0; i < shellPoolSize; i++)
            {
                GameObject shell = Instantiate(shellPrefab, shellParent.transform);
                shell.SetActive(false);
                shellPool.Enqueue(shell);
            }
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
        currentHoldingInteractor = args.interactorObject as XRBaseInputInteractor;

        if ((!IsSpawned || IsOwner) && magazinePrefab != null)
        {
            AmmoPouch localPouch = FindObjectOfType<AmmoPouch>();
            if (localPouch != null)
            {
                localPouch.SetMagazinePrefab(magazinePrefab);
            }
        }
    }

    private void OnWeaponReleased(SelectExitEventArgs args)
    {
        isHeld = false;
        currentHoldingInteractor = null;
    }

    private void OnTriggerPulled(ActivateEventArgs args)
    {
        if (IsSpawned && !IsOwner) return;
        
        if (IsSpawned) FireWeaponServerRpc();
        else FireWeaponLocal();
    }

    [ServerRpc]
    private void FireWeaponServerRpc()
    {
        FireWeaponLocal();
    }

    private void FireWeaponLocal()
    {
        if (chamberState.Value != ChamberState.LiveRound)
        {
            if (IsSpawned) PlayDryFireClientRpc();
            else PlayDryFireLocal();
            return;
        }

        chamberState.Value = ChamberState.SpentShell;

        // Perform Raycasts on the server for authority (or locally if offline)
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 spreadDirection = barrelPoint.forward;
            
            // Random cone spread
            Vector3 randomPoint = Random.insideUnitSphere;
            spreadDirection = Quaternion.AngleAxis(Random.Range(0f, spreadAngle), randomPoint) * spreadDirection;

            if (Physics.Raycast(barrelPoint.position, spreadDirection, out RaycastHit hit, range, hitMask))
            {
                SurfaceType hitType = SurfaceType.Default;
                HittableSurface hittable = hit.collider.GetComponentInParent<HittableSurface>();
                if (hittable != null)
                {
                    hitType = hittable.surfaceType;
                    hittable.OnHit(hit);
                }
                
                SpawnDecalLocal(hit.point, hit.normal, hitType, hit.collider.transform);
                if (IsSpawned) SpawnDecalRpc(hit.point, hit.normal, hitType);
            }
        }

        if (IsSpawned) PlayShootEffectsClientRpc();
        else PlayShootEffectsLocal();
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

    [ClientRpc]
    private void PlayDryFireClientRpc()
    {
        PlayDryFireLocal();
    }

    private void PlayDryFireLocal()
    {
        if (audioSource != null && dryFireSound != null)
            audioSource.PlayOneShot(dryFireSound, shootVolume);
    }

    [ClientRpc]
    private void PlayShootEffectsClientRpc()
    {
        PlayShootEffectsLocal();
    }

    private void PlayShootEffectsLocal()
    {
        if (muzzleFlash != null) muzzleFlash.Play(true);
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
            if (IsSpawned) EjectShellServerRpc();
            else EjectShellLocal();
        }

        chamberState.Value = ChamberState.Empty;
        
        if (IsSpawned) PlaySoundClientRpc(true);
        else PlaySoundLocal(true);
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

        if (IsSpawned) PlaySoundClientRpc(false);
        else PlaySoundLocal(false);
    }

    [ServerRpc]
    private void EjectShellServerRpc()
    {
        EjectShellClientRpc();
    }

    [ClientRpc]
    private void EjectShellClientRpc()
    {
        EjectShellLocal();
    }

    private void EjectShellLocal()
    {
        if (shellEjectionPoint == null || shellPrefab == null || shellPool == null || shellPool.Count == 0) return;

        GameObject shell = shellPool.Dequeue();
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

    [ClientRpc]
    private void PlaySoundClientRpc(bool isPullBack)
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
