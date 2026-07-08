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
        // StartCoroutine(TryFindSocketAndSlotRoutine());
        
        // Instead of forcefully teleporting and bypassing XRI, 
        // we just freeze the weapon in mid-air exactly where the spawner placed it.
        // The XRSocketInteractor will naturally detect it inside its trigger collider
        // and safely grab it on the next physics frame!
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // We also disable the NetworkPhysicsInteractable spawn lock so it doesn't fight the socket
        var netPhysics = GetComponent<XRMultiplayer.NetworkPhysicsInteractable>();
        if (netPhysics != null)
        {
            netPhysics.spawnLocked = false;
        }
    }

    private IEnumerator TryFindSocketAndSlotRoutine()
    {
        PlayerHolsterSystem myHolsters = null;
        
        bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;

        // Give the network up to 5 seconds to fully sync the player rig to the clients!
        float timeout = 5f;
        while (myHolsters == null && timeout > 0f)
        {
            PlayerHolsterSystem[] allHolsters = FindObjectsOfType<PlayerHolsterSystem>();
            
            foreach (var holsters in allHolsters)
            {
                if (isOffline)
                {
                    // Offline Mode: Just grab the only holster in the scene!
                    myHolsters = holsters;
                    break;
                }
                else
                {
                    // Online Mode: Strictly find the holster that shares our EXACT Player ID!
                    NetworkObject holsterNetObj = holsters.GetComponentInParent<NetworkObject>();
                    if (holsterNetObj != null && holsterNetObj.OwnerClientId == this.OwnerClientId)
                    {
                        myHolsters = holsters;
                        break;
                    }
                }
            }

            if (myHolsters == null)
            {
                // Wait briefly and try searching again
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
                Debug.Log($"<color=yellow>[WeaponAutoReturn]</color> Found home socket for {gameObject.name}. Forcing slot routine...");
                yield return StartCoroutine(TryFindSocketAndSlotRoutine());
            }
            else
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn]</color> PlayerHolsterSystem was found, but the {slotType} socket was NULL!");
            }
        }
        else
        {
            Debug.LogError($"<color=red>[WeaponAutoReturn]</color> TIMEOUT: Could not find a PlayerHolsterSystem in the scene that belongs to Player ID {OwnerClientId}!");
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // If someone grabs it, stop the return timer!
        if (returnRoutine != null) StopCoroutine(returnRoutine);
    }

    private void OnDropped(SelectExitEventArgs args)
    {
        if (IsSpawned && !IsOwner) return;

        // If it was dropped on the ground (NOT put into another socket)
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
            yield return StartCoroutine(TryFindSocketAndSlotRoutine());
        }
    }
}
