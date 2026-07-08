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
    public XRSocketInteractor homeSocket;
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
        // IMMEDIATELY FREEZE so they don't fall through the floor while we wait for the spawner to wire us up!
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
        // Wait until the PlayerWeaponSpawner injects our homeSocket!
        float timeout = 2f;
        while (homeSocket == null && timeout > 0f)
        {
            yield return new WaitForEndOfFrame();
            timeout -= Time.deltaTime;
        }

        if (homeSocket == null)
        {
            // If the spawner didn't inject it (maybe the user spawned it manually from an admin menu?)
            // Fallback: manually find our holster!
            PlayerHolsterSystem[] allHolsters = FindObjectsOfType<PlayerHolsterSystem>();
            foreach (var holsters in allHolsters)
            {
                NetworkObject holsterNetObj = holsters.GetComponentInParent<NetworkObject>();
                if (holsterNetObj == null) 
                {
                    // It's the local VR rig!
                    if (slotType == WeaponSlotType.Rifle) homeSocket = holsters.rightShoulderSocket;
                    else if (slotType == WeaponSlotType.Shotgun) homeSocket = holsters.leftShoulderSocket;
                    else if (slotType == WeaponSlotType.Pistol) homeSocket = holsters.rightBeltSocket;
                    break;
                }
            }
        }

            if (homeSocket != null)
            {
                var netPhysics = GetComponent<XRMultiplayer.NetworkPhysicsInteractable>();
                if (netPhysics != null) netPhysics.spawnLocked = false;

                // Stop our script from fighting XRI
                grabInteractable.enabled = false;
                yield return new WaitForEndOfFrame();

                if (grabInteractable.interactionManager != homeSocket.interactionManager)
                {
                    grabInteractable.interactionManager = homeSocket.interactionManager;
                }

                // Snap the physical position immediately
                Transform attach = homeSocket.attachTransform != null ? homeSocket.attachTransform : homeSocket.transform;
                transform.position = attach.position;
                transform.rotation = attach.rotation;

                // Lock it completely so XRI doesn't fumble the grab
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                grabInteractable.enabled = true;
                yield return new WaitForEndOfFrame();

                // Logically force the socket to grab the weapon!
                try
                {
                    homeSocket.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)homeSocket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
                    Debug.Log($"<color=green>[WeaponAutoReturn]</color> Logically spawned and locked {gameObject.name} into {homeSocket.name}!");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=red>[WeaponAutoReturn]</color> SelectEnter failed: {e.Message}");
                }
            }
    }

    private void Update()
    {
        // The Update loop is no longer needed since we are using logical SelectEnter!
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
            
            // Logically return the weapon directly to the socket!
            grabInteractable.enabled = false;
            yield return new WaitForEndOfFrame();

            if (grabInteractable.interactionManager != homeSocket.interactionManager)
            {
                grabInteractable.interactionManager = homeSocket.interactionManager;
            }

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

            grabInteractable.enabled = true;
            yield return new WaitForEndOfFrame();

            try
            {
                homeSocket.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)homeSocket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"<color=red>[WeaponAutoReturn]</color> SelectEnter failed during auto-return: {e.Message}");
            }
        }
    }
}
