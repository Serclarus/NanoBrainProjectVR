using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using Unity.Netcode;

public enum WeaponSlotType { Rifle, Shotgun, Pistol }

public class WeaponAutoReturn : NetworkBehaviour
{
    [Header("Weapon Configuration")]
    [Tooltip("Which socket on the player's body does this weapon belong to?")]
    public WeaponSlotType slotType;
    
    [Tooltip("How many seconds the weapon sits on the ground before returning")]
    public float returnDelay = 3.0f;

    private XRGrabInteractable grabInteractable;
    private XRSocketInteractor homeSocket;
    private Coroutine returnRoutine;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnDropped);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnDropped);
        }
    }

    private void Start()
    {
        // If we are testing offline in singleplayer (network is not running), slot immediately!
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            Debug.Log($"<color=yellow>[WeaponAutoReturn]</color> Offline mode detected! Slotting {gameObject.name} immediately.");
            TryFindSocketAndSlot();
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"<color=yellow>[WeaponAutoReturn]</color> OnNetworkSpawn called for {gameObject.name}. IsOwner: {IsOwner}");
        
        // When spawned online, only slot it for the owner!
        if (IsOwner)
        {
            TryFindSocketAndSlot();
        }
    }

    private void TryFindSocketAndSlot()
    {
        // IMMEDIATELY FREEZE so they don't fall through the floor while we search!
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        StartCoroutine(NativeSlotRoutine());
    }

    private IEnumerator NativeSlotRoutine()
    {
        PlayerHolsterSystem myHolsters = null;
        bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        float timeout = 5f;

        while (myHolsters == null && timeout > 0f)
        {
            PlayerHolsterSystem[] allHolsters = FindObjectsOfType<PlayerHolsterSystem>();
            foreach (var holsters in allHolsters)
            {
                if (isOffline)
                {
                    myHolsters = holsters;
                    break;
                }
                else
                {
                    NetworkObject holsterNetObj = holsters.GetComponentInParent<NetworkObject>();
                    
                    // If the holster has a NetworkObject, check if we own it
                    if (holsterNetObj != null && holsterNetObj.OwnerClientId == this.OwnerClientId)
                    {
                        myHolsters = holsters;
                        break;
                    }
                    // If the holster has NO NetworkObject, it must be the local VR Rig!
                    // If we are the owner of this weapon, this local rig is ours!
                    else if (holsterNetObj == null && IsOwner)
                    {
                        myHolsters = holsters;
                        break;
                    }
                }
            }

            if (myHolsters == null)
            {
                yield return new WaitForSeconds(0.2f);
                timeout -= 0.2f;
            }
        }

        if (myHolsters != null)
        {
            if (slotType == WeaponSlotType.Rifle) homeSocket = myHolsters.rightShoulderSocket;
            else if (slotType == WeaponSlotType.Shotgun) homeSocket = myHolsters.leftShoulderSocket;
            else if (slotType == WeaponSlotType.Pistol) homeSocket = myHolsters.rightBeltSocket;

            if (homeSocket != null)
            {
                // Un-lock network spawn constraints so the socket can move it
                var netPhysics = GetComponent<XRMultiplayer.NetworkPhysicsInteractable>();
                if (netPhysics != null) netPhysics.spawnLocked = false;

                // Make the weapon physics-dead so it CANNOT fall
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.constraints = RigidbodyConstraints.None;
                }

                // Snap perfectly to the socket attach transform so the Trigger detects it
                Transform attach = homeSocket.attachTransform != null ? homeSocket.attachTransform : homeSocket.transform;
                transform.position = attach.position;
                transform.rotation = attach.rotation;
                
                Debug.Log($"<color=cyan>[WeaponAutoReturn]</color> Snapped {gameObject.name} perfectly to {homeSocket.name}. Waiting for native XR socket grab...");
            }
        }
    }

    private void Update()
    {
        // If we have a home socket but XRI hasn't organically grabbed us yet, 
        // FORCE our position to stay exactly inside the socket's trigger so it CAN'T miss us!
        if (homeSocket != null && !homeSocket.hasSelection && grabInteractable != null && !grabInteractable.isSelected)
        {
            Transform attach = homeSocket.attachTransform != null ? homeSocket.attachTransform : homeSocket.transform;
            transform.position = attach.position;
            transform.rotation = attach.rotation;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.isKinematic = true; // Reinforce gravity lock
            }
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (returnRoutine != null) StopCoroutine(returnRoutine);
    }

    private void OnDropped(SelectExitEventArgs args)
    {
        if (IsSpawned && !IsOwner) return;

        if (!(args.interactorObject is XRSocketInteractor))
        {
            if (returnRoutine != null) StopCoroutine(returnRoutine);
            returnRoutine = StartCoroutine(ReturnTimer());
        }
    }

    private IEnumerator ReturnTimer()
    {
        yield return new WaitForSeconds(returnDelay);

        if (homeSocket != null)
        {
            Debug.Log($"Weapon Auto-Returned to {slotType} Holster!");
            
            // Just drop it exactly on the socket attach transform and let XRI grab it natively again!
            Transform attach = homeSocket.attachTransform != null ? homeSocket.attachTransform : homeSocket.transform;
            transform.position = attach.position;
            transform.rotation = attach.rotation;
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
