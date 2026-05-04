using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Needed for XRI 3.0+
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.Netcode;

[RequireComponent(typeof(XRGrabInteractable))]
public class Magazine : NetworkBehaviour
{
    [Header("Ammo Settings")]
    [Tooltip("Maximum amount of bullets this magazine can hold.")]
    public int maxAmmo = 30;

    [Tooltip("Initial amount of bullets in the magazine.")]
    public int initialAmmo = 30;
    
    [HideInInspector]
    public NetworkVariable<int> currentAmmo = new NetworkVariable<int>(30, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Tooltip("If true, the magazine will never run out of ammo.")]
    public bool infiniteAmmo = false;

    [Header("Cleanup Settings")]
    [Tooltip("How many seconds until the magazine destroys itself after being dropped empty?")]
    public float despawnDelay = 10f;

    [Header("Visuals")]
    [Tooltip("The bullet object to hide when the magazine is empty. Leaves empty to auto-detect a child named 'Bullet'.")]
    public GameObject topBulletVisual;

    private XRGrabInteractable grabInteractable;
    private Coroutine despawnCoroutine;
    private bool isHeld = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrabbedOrSocketed);
        grabInteractable.selectExited.AddListener(OnDropped);
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbedOrSocketed);
            grabInteractable.selectExited.RemoveListener(OnDropped);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            if (initialAmmo > maxAmmo)
            {
                currentAmmo.Value = maxAmmo;
            }
            else
            {
                currentAmmo.Value = initialAmmo;
            }
        }
        
        currentAmmo.OnValueChanged += OnAmmoChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentAmmo.OnValueChanged -= OnAmmoChanged;
    }

    private void Start()
    {
        UpdateVisuals();
        CheckDespawnCondition(); // Run check in case it spawns totally empty on the floor
    }

    private void OnAmmoChanged(int previousValue, int newValue)
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (topBulletVisual != null)
        {
            topBulletVisual.SetActive(HasAmmo());
        }
    }

    private void OnGrabbedOrSocketed(SelectEnterEventArgs args)
    {
        isHeld = true;
        // Instantly cancel despawn if someone picks it up or sockets it
        if (despawnCoroutine != null)
        {
            StopCoroutine(despawnCoroutine);
            despawnCoroutine = null;
        }
    }

    private void OnDropped(SelectExitEventArgs args)
    {
        isHeld = false;

        // Force physics back on when dropped into the world (not into a socket).
        // This fixes mags floating after being pulled from the ammo pouch socket,
        // because XRI may remember the kinematic state the socket set.
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

        CheckDespawnCondition();
    }

    private void CheckDespawnCondition()
    {
        if (!isHeld && despawnCoroutine == null)
        {
            despawnCoroutine = StartCoroutine(DespawnRoutine());
        }
    }

    private IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(despawnDelay);
        if (IsSpawned)
        {
            if (IsOwner)
            {
                RequestDespawnRpc();
            }
        }
        else
        {
            Destroy(gameObject); // Fallback if not networked yet
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestDespawnRpc()
    {
        NetworkObject.Despawn();
    }

    /// <summary>
    /// Checks if there is at least one bullet left in the magazine.
    /// </summary>
    public bool HasAmmo()
    {
        if (infiniteAmmo) return true;
        return currentAmmo.Value > 0;
    }

    /// <summary>
    /// Removes one bullet from the magazine if available.
    /// Returns true if a bullet was successfully consumed, false if empty.
    /// </summary>
    public bool ConsumeAmmo()
    {
        if (!IsOwner && IsSpawned) return false;

        if (infiniteAmmo) return true;

        if (currentAmmo.Value > 0)
        {
            currentAmmo.Value--;
            
            // If we just shot the last bullet, check if we immediately need to start the despawn timer (if it's not held/socketed somehow)
            if (currentAmmo.Value <= 0)
            {
                UpdateVisuals();
                CheckDespawnCondition();
            }

            return true;
        }
        return false;
    }
}
