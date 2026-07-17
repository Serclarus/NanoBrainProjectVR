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
        
        // IMMEDIATELY FREEZE ON EVERYONE'S SCREEN so it doesn't fall through the floor on the Server!
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

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
            PlayerHolsterSystem[] allHolsters = FindObjectsOfType<PlayerHolsterSystem>();
            
            // First pass: prioritize our OWN NETWORKED holsters!
            foreach (var holsters in allHolsters)
            {
                NetworkObject holsterNetObj = holsters.GetComponentInParent<NetworkObject>();
                if (holsterNetObj != null && holsterNetObj.IsOwner) 
                {
                    if (slotType == WeaponSlotType.Rifle) homeSocket = holsters.rightShoulderSocket;
                    else if (slotType == WeaponSlotType.Shotgun) homeSocket = holsters.leftShoulderSocket;
                    else if (slotType == WeaponSlotType.Pistol) homeSocket = holsters.rightBeltSocket;
                    break;
                }
            }

            // Second pass: if we have no networked rig, fall back to offline rig (for singleplayer testing)
            if (homeSocket == null)
            {
                foreach (var holsters in allHolsters)
                {
                    NetworkObject holsterNetObj = holsters.GetComponentInParent<NetworkObject>();
                    if (holsterNetObj == null) 
                    {
                        if (slotType == WeaponSlotType.Rifle) homeSocket = holsters.rightShoulderSocket;
                        else if (slotType == WeaponSlotType.Shotgun) homeSocket = holsters.leftShoulderSocket;
                        else if (slotType == WeaponSlotType.Pistol) homeSocket = holsters.rightBeltSocket;
                        break;
                    }
                }
            }
        }

        // Failsafe: if we STILL couldn't find holsters, teleport the weapon in front of the player's face so they can at least see it spawned!
        if (homeSocket == null && Camera.main != null)
        {
            transform.position = Camera.main.transform.position + Camera.main.transform.forward * 1.0f;
            Debug.Log($"<color=red>[WeaponAutoReturn]</color> FAILED to find holsters for {gameObject.name}! Teleported in front of face.");
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

    public void ForceReturnToSocket()
    {
        if (returnRoutine != null) StopCoroutine(returnRoutine);
        returnRoutine = StartCoroutine(ReturnTimer(0f));
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

    private IEnumerator ReturnTimer(float delay = -1f)
    {
        yield return new WaitForSeconds(delay >= 0f ? delay : returnDelay);

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

            // CRITICAL FIX: Explicitly teleport all socketed items (like magazines) so they don't get left behind during scene transitions!
            foreach (var socket in GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>())
            {
                if (socket.hasSelection)
                {
                    var magInteractable = socket.interactablesSelected[0];
                    Transform attachedObj = magInteractable.transform;
                    Transform targetAttach = socket.attachTransform != null ? socket.attachTransform : socket.transform;
                    attachedObj.position = targetAttach.position;
                    attachedObj.rotation = targetAttach.rotation;
                    
                    Rigidbody attachedRb = attachedObj.GetComponent<Rigidbody>();
                    if (attachedRb != null)
                    {
                        attachedRb.isKinematic = true;
                        attachedRb.linearVelocity = Vector3.zero;
                        attachedRb.angularVelocity = Vector3.zero;
                    }

                    if (homeSocket != null && homeSocket.interactionManager != null)
                    {
                        socket.interactionManager = homeSocket.interactionManager;
                        
                        var grabInteractableMag = attachedObj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                        if (grabInteractableMag != null)
                        {
                            grabInteractableMag.interactionManager = homeSocket.interactionManager;
                            
                            try 
                            {
                                socket.interactionManager.SelectEnter((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)socket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractableMag);
                            }
                            catch (System.Exception e) { }
                        }
                    }
                }
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
