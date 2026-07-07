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
        StartCoroutine(TryFindSocketAndSlotRoutine());
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
                yield return StartCoroutine(ForceSlotWeaponRoutine());
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

    private IEnumerator ForceSlotWeaponRoutine()
    {
        // INSTANT PHYSICS FREEZE AND INTERACTION DISABLE:
        Rigidbody rb = GetComponent<Rigidbody>();
        bool wasKinematic = false;
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None; // Unlock FreezeAll from spawnLocked!
        }

        // DISABLE ClientNetworkTransform so it stops fighting the socket position!
        var clientNetTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        bool hadNetTransform = false;
        if (clientNetTransform != null)
        {
            hadNetTransform = clientNetTransform.enabled;
            clientNetTransform.enabled = false;
        }

        // Unlock spawnLocked on NetworkPhysicsInteractable so constraints don't re-freeze
        var netPhysics = GetComponent<XRMultiplayer.NetworkPhysicsInteractable>();
        if (netPhysics != null)
        {
            netPhysics.spawnLocked = false;
        }
        
        // COMPLETELY DISABLE THE GRAB SO NO OTHER SOCKET CAN STEAL IT!
        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        }

        // Wait briefly for the network and physics to initialize
        yield return new WaitForSeconds(0.35f);
        
        if (homeSocket != null)
        {
            if (homeSocket.interactionManager == null)
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn]</color> {gameObject.name} teleported to {homeSocket.name}, but the socket has NO Interaction Manager assigned! It will fall to the floor!");
                if (rb != null) rb.isKinematic = wasKinematic;
                if (grabInteractable != null) grabInteractable.enabled = true;
                if (clientNetTransform != null) clientNetTransform.enabled = hadNetTransform;
                yield break;
            }

            // Force the weapon to use the EXACT same Interaction Manager as the socket.
            if (grabInteractable.interactionManager != homeSocket.interactionManager)
            {
                grabInteractable.interactionManager = homeSocket.interactionManager;
                homeSocket.interactionManager.RegisterInteractable((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable)grabInteractable);
            }

            // RE-ENABLE THE GRAB JUST BEFORE SOCKETING!
            if (grabInteractable != null)
            {
                grabInteractable.enabled = true;
            }

            // Teleport the weapon directly to the socket's location
            transform.position = homeSocket.transform.position;
            transform.rotation = homeSocket.transform.rotation;

            // Restore physics to XRI's control exactly before we slot it
            if (rb != null) rb.isKinematic = wasKinematic;

            Debug.Log($"<color=cyan>[WeaponAutoReturn]</color> Teleporting {gameObject.name} to {homeSocket.name} at World Position: {homeSocket.transform.position}. Forcing SelectEnter...");
            
            try
            {
                homeSocket.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)homeSocket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn] CRASH!</color> Exception thrown during SelectEnter for {gameObject.name}: {e.Message}\n{e.StackTrace}");
            }

            // Wait 1 frame for XRI to update, THEN re-enable network transform so it syncs the correct socketed position
            yield return new WaitForEndOfFrame();

            if (clientNetTransform != null)
            {
                clientNetTransform.enabled = true;
            }
            
            if (!homeSocket.hasSelection || (UnityEngine.Object)homeSocket.interactablesSelected[0] != (UnityEngine.Object)grabInteractable)
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn] REJECTION!</color> {gameObject.name} tried to slot into {homeSocket.name}, but the Socket REJECTED IT! Please check your 'Interaction Layer Mask' on the {homeSocket.name}!");
            }
            else
            {
                Debug.Log($"<color=green>[WeaponAutoReturn]</color> SUCCESS! {gameObject.name} is securely locked inside {homeSocket.name}!");
            }
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
            yield return StartCoroutine(ForceSlotWeaponRoutine());
        }
    }
}
