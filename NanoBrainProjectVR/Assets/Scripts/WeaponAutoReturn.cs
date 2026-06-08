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
        PlayerHolsterSystem holsters = FindObjectOfType<PlayerHolsterSystem>();
        if (holsters != null)
        {
            if (slotType == WeaponSlotType.Rifle) homeSocket = holsters.rightShoulderSocket;
            else if (slotType == WeaponSlotType.Shotgun) homeSocket = holsters.leftShoulderSocket;
            else if (slotType == WeaponSlotType.Pistol) homeSocket = holsters.rightBeltSocket;

            if (homeSocket != null)
            {
                Debug.Log($"<color=yellow>[WeaponAutoReturn]</color> Found home socket for {gameObject.name}. Forcing slot routine...");
                StartCoroutine(ForceSlotWeaponRoutine());
            }
            else
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn]</color> PlayerHolsterSystem was found, but the {slotType} socket was NULL!");
            }
        }
        else
        {
            Debug.LogError($"<color=red>[WeaponAutoReturn]</color> Could not find PlayerHolsterSystem in the scene!");
        }
    }

    private IEnumerator ForceSlotWeaponRoutine()
    {
        // Wait briefly for the network and physics to initialize
        yield return new WaitForSeconds(0.25f);
        
        if (homeSocket != null)
        {
            if (homeSocket.interactionManager == null)
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn]</color> The home socket has NO Interaction Manager assigned! XRI requires an Interaction Manager.");
                yield break;
            }

            // Reset velocity
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Teleport the weapon directly to the socket
            transform.position = homeSocket.transform.position;
            transform.rotation = homeSocket.transform.rotation;

            Debug.Log($"<color=yellow>[WeaponAutoReturn]</color> Forcing SelectEnter on {homeSocket.name} with {grabInteractable.name}");
            
            // Force the Interaction Manager to link them
            homeSocket.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)homeSocket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
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
