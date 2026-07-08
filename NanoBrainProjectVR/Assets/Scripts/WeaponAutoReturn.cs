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
                    if (holsterNetObj != null && holsterNetObj.OwnerClientId == this.OwnerClientId)
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
                yield return StartCoroutine(ForceSlotWeaponRoutine());
            }
            else
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn]</color> {slotType} socket was NULL!");
            }
        }
        else
        {
            Debug.LogError($"<color=red>[WeaponAutoReturn]</color> TIMEOUT: Could not find PlayerHolsterSystem!");
        }
    }

    private IEnumerator ForceSlotWeaponRoutine()
    {
        // INSTANT PHYSICS FREEZE
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.None; // Unlock FreezeAll
        }

        var netPhysics = GetComponent<XRMultiplayer.NetworkPhysicsInteractable>();
        if (netPhysics != null) netPhysics.spawnLocked = false;

        if (grabInteractable != null) grabInteractable.enabled = false;

        yield return new WaitForSeconds(0.35f);

        if (homeSocket != null)
        {
            if (homeSocket.interactionManager == null)
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn]</color> {homeSocket.name} has NO Interaction Manager assigned! Please assign it in the Inspector.");
                if (grabInteractable != null) grabInteractable.enabled = true;
                yield break;
            }

            if (grabInteractable.interactionManager != homeSocket.interactionManager)
            {
                grabInteractable.interactionManager = homeSocket.interactionManager;
                homeSocket.interactionManager.RegisterInteractable((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable)grabInteractable);
            }

            if (grabInteractable != null) grabInteractable.enabled = true;

            transform.position = homeSocket.transform.position;
            transform.rotation = homeSocket.transform.rotation;

            if (rb != null) 
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true; 
            }

            try
            {
                homeSocket.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)homeSocket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
                Debug.Log($"<color=cyan>[WeaponAutoReturn]</color> Forced SelectEnter for {gameObject.name}.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn] CRASH!</color> {e.Message}");
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
            yield return StartCoroutine(ForceSlotWeaponRoutine());
        }
    }
}
