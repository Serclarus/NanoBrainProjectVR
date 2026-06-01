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
            grabInteractable.activated.AddListener(OnTriggerPulled);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnWeaponGrabbed);
            grabInteractable.activated.RemoveListener(OnTriggerPulled);
        }
    }

    private void OnWeaponGrabbed(SelectEnterEventArgs args)
    {
        currentHoldingInteractor = args.interactorObject as XRBaseInputInteractor;

        if (IsOwner && magazinePrefab != null)
        {
            AmmoPouch localPouch = FindObjectOfType<AmmoPouch>();
            if (localPouch != null)
            {
                localPouch.SetMagazinePrefab(magazinePrefab);
            }
        }
    }

    private void OnTriggerPulled(ActivateEventArgs args)
    {
        if (!IsOwner) return;
        FireWeaponServerRpc();
    }

    [ServerRpc]
    private void FireWeaponServerRpc()
    {
        if (chamberState.Value != ChamberState.LiveRound)
        {
            PlayDryFireClientRpc();
            return;
        }

        chamberState.Value = ChamberState.SpentShell;

        // Perform Raycasts on the server for authority
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 spreadDirection = barrelPoint.forward;
            
            // Random cone spread
            Vector3 randomPoint = Random.insideUnitSphere;
            spreadDirection = Quaternion.AngleAxis(Random.Range(0f, spreadAngle), randomPoint) * spreadDirection;

            if (Physics.Raycast(barrelPoint.position, spreadDirection, out RaycastHit hit, range, hitMask))
            {
                HittableSurface hittable = hit.collider.GetComponentInParent<HittableSurface>();
                if (hittable != null) hittable.OnHit(hit);
            }
        }

        PlayShootEffectsClientRpc();
    }

    [ClientRpc]
    private void SpawnDecalClientRpc(Vector3 point, Vector3 normal)
    {
        // TODO: Implement Object Pooled Decals here later!
    }

    [ClientRpc]
    private void PlayDryFireClientRpc()
    {
        if (audioSource != null && dryFireSound != null)
            audioSource.PlayOneShot(dryFireSound, shootVolume);
    }

    [ClientRpc]
    private void PlayShootEffectsClientRpc()
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
        if (!IsOwner) return;

        if (chamberState.Value == ChamberState.SpentShell || chamberState.Value == ChamberState.LiveRound)
        {
            EjectShellServerRpc();
        }

        chamberState.Value = ChamberState.Empty;
        PlaySoundClientRpc(true);
    }

    // Call this via UnityEvent on the TwoHandGrabInteractable!
    public void OnPumpPushedForward()
    {
        if (!IsOwner) return;

        if (chamberState.Value == ChamberState.Empty && currentAmmo.Value > 0)
        {
            currentAmmo.Value--;
            chamberState.Value = ChamberState.LiveRound;
        }

        PlaySoundClientRpc(false);
    }

    [ServerRpc]
    private void EjectShellServerRpc()
    {
        EjectShellClientRpc();
    }

    [ClientRpc]
    private void EjectShellClientRpc()
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
