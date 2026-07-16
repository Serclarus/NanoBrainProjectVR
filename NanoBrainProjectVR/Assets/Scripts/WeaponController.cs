using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit; // Required for XR Grab Interactable
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Required for XRSocketInteractor
using Unity.Netcode; // Required for NGO

[System.Serializable]
public struct RecoilTier
{
    [Tooltip("Min and Max upward kick (Pitch)")]
    public Vector2 pitchRange;
    [Tooltip("Side to side wobble per shot (Yaw)")]
    public Vector2 yawRange;
    [Tooltip("Rotational twist per shot (Roll)")]
    public Vector2 rollRange;
    [Tooltip("How far the gun kicks back per shot")]
    public float backwardKick;
}

[RequireComponent(typeof(TwoHandGrabInteractable))]
public class WeaponController : NetworkBehaviour
{
    [Header("Fire Mode")]
    [Tooltip("If true, holding the trigger will fire continuously. If false, one shot per trigger pull.")]
    public bool fullAuto = true;
    [Tooltip("Rounds per minute for full-auto fire")]
    public float fireRate = 600f;
    public float range = 100f;

    [Header("Audio Settings")]
    public AudioClip shootSound;
    public AudioClip dryFireSound;
    public Vector2 soundPitchRange = new Vector2(0.95f, 1.05f);
    [Range(0f, 1f)] public float shootVolume = 1f;

    [Header("Controller Haptics")]
    [Range(0f, 1f)] public float hapticIntensity = 0.5f;
    public float hapticDuration = 0.1f;

    [Header("Shell Ejection")]
    public GameObject shellPrefab;
    public float shellEjectionForce = 3f;
    public float shellTorque = 1f;

    [Header("Recoil Tiers")]
    public RecoilTier tier1_Shots1to3 = new RecoilTier { pitchRange = new Vector2(1.0f, 2.0f), yawRange = new Vector2(-0.3f, 0.3f), rollRange = new Vector2(-0.2f, 0.2f), backwardKick = 0.01f };
    public RecoilTier tier2_Shots4to8 = new RecoilTier { pitchRange = new Vector2(2.0f, 4.0f), yawRange = new Vector2(-1.0f, 1.0f), rollRange = new Vector2(-0.5f, 0.5f), backwardKick = 0.025f };
    public RecoilTier tier3_Shots9to17 = new RecoilTier { pitchRange = new Vector2(3.0f, 6.0f), yawRange = new Vector2(-2.0f, 2.0f), rollRange = new Vector2(-0.8f, 0.8f), backwardKick = 0.04f };
    public RecoilTier tier4_Shots18plus = new RecoilTier { pitchRange = new Vector2(4.0f, 8.0f), yawRange = new Vector2(-3.0f, 3.0f), rollRange = new Vector2(-1.0f, 1.0f), backwardKick = 0.05f };

    [Header("Recoil Dynamics")]
    public float snappiness = 20f;
    public float returnSpeed = 8f;
    public float twoHandRecoilModifier = 0.4f;

    [Header("Smart Ammo Pouch")]
    [Tooltip("The magazine prefab that your Smart Vest Pouch will dispense when holding this weapon!")]
    public GameObject magazinePrefab;

    private TwoHandGrabInteractable grabInteractable;

    [Header("Shooting Setup")]
    [Tooltip("Which layers should the bullet hit?")]
    public LayerMask hitMask = ~0; 
    [Tooltip("The point where the raycast bullet originates")]
    public Transform barrelPoint;

    // Events for Training Grounds
    public static event System.Action<int> OnProjectilesFired;

    [Header("Damage Settings")]
    public float weaponDamage = 50f;
    [Tooltip("How far (in meters) the bullet can penetrate into targets before stopping.")]
    public float penetrationDepth = 0.5f;

    [Header("Fire Mode")]
    private float fireCooldownTimer = 0f;
    private bool isTriggerHeld = false;
    private bool isHeld = false;
    private bool hasPlayedDryFire = false;

    [Header("Trigger Animation")]
    [Tooltip("The trigger bone/transform on the weapon model")]
    public Transform triggerTransform;
    [Tooltip("Maximum rotation angle when the trigger is fully pulled (degrees). Positive = rotates on local X since the asset is backwards.")]
    public float triggerMaxAngle = 15f;
    [Tooltip("The axis around which the trigger rotates locally")]
    public Vector3 triggerRotateAxis = Vector3.right;
    private Quaternion triggerOriginalRotation;
    private XRBaseInputInteractor currentHoldingInteractor;

    [Header("Ammo & Reloading Setup")]
    [Tooltip("If true, the weapon will automatically spawn a magazine inside its socket when the game starts.")]
    public bool spawnWithMagazine = true;

    [Tooltip("The socket interactor that holds the magazine")]
    public XRSocketInteractor magazineSocket;
    
    [Tooltip("An offset applied to the shell when it spawns, to correct shell models that are oriented vertically")]
    public Vector3 shellSpawnRotationOffset = Vector3.zero;

    [Tooltip("If true, the slide locks backwards when the magazine is empty (used for Pistols, disable for Rifles)")]
    public bool hasSlideLockOnEmpty = true;
    [Tooltip("How far back the slide goes when locked back on empty. Usually smaller than boltTravelDistance.")]
    public float slideLockDistance = 0.03f;
    public NetworkVariable<bool> isSlideLockedBack = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    private Magazine currentMagazine;
    public NetworkVariable<NetworkObjectReference> syncedMagazine = new NetworkVariable<NetworkObjectReference>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> syncedIsHeld = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    public NetworkVariable<bool> isChambered = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [Header("Visual Effects")]
    [Tooltip("Assign the Muzzle Flash GameObject here (it should be a child of the weapon). All Particle Systems on this object and its children will be played.")]
    public GameObject muzzleFlash; 

    [Header("Audio Settings")]
    [Tooltip("The AudioSource component used to play the sound")]
    public AudioSource audioSource;

    [Header("Bolt Animation & Shell Ejection")]
    [Tooltip("The moving part of the weapon (the bolt or slide)")]
    public Transform boltTransform;
    [Tooltip("How far the bolt moves backwards on the Z axis")]
    public float boltTravelDistance = 0.05f;
    [Tooltip("How fast the bolt snaps back")]
    public float boltSnappiness = 30f;
    [Tooltip("How fast the bolt springs forward")]
    public float boltReturnSpeed = 15f;

    [Tooltip("The point where shells are ejected from")]
    public Transform shellEjectionPoint;
    [Tooltip("How many shells to keep in the pool")]
    public int shellPoolSize = 15;
    [Tooltip("How long a shell lives before being disabled")]
    public float shellLifeTime = 4f;

    [HideInInspector] public Vector3 originalBoltPosition;
    [HideInInspector] public float currentBoltOffset;
    [HideInInspector] public float targetBoltOffset;
    [HideInInspector] public bool isSlideGrabbed = false;
    private bool hasEjectedShell = true;

    private Queue<GameObject> shellPool;
    private Dictionary<GameObject, Coroutine> activeShellCoroutines;

    [Header("Interactables Group")]
    [Tooltip("Assign the parent GameObject that holds all sub-interactables (Slide, Magazine Socket, etc.). It will be disabled when the weapon is dropped to prevent accidental grabs.")]
    public GameObject subInteractablesGroup;

    [Header("Procedural Recoil Settings")]
    [Tooltip("Assign the visual model wrapper of the gun here. Rotating the root will conflict with VR Tracking!")]
    public Transform weaponModel;

    private int consecutiveShots = 0;
    
    private Vector3 originalModelRotation;
    private Vector3 originalModelPosition;

    // Variables for procedural spring math
    private Vector3 currentRotation;
    private Vector3 targetRotation;
    private Vector3 currentPosition;
    private Vector3 targetPosition;

    private void Awake()
    {
        SetSubInteractablesState(false);

        grabInteractable = GetComponent<TwoHandGrabInteractable>();
        
        if (grabInteractable != null)
        {
            // VERY IMPORTANT FOR MULTIPLAYER!
            // If XR Interaction Toolkit reparents a NetworkObject to a hand or socket locally, Unity Netcode panics and despawns it for all other players!
            // This forces XRI to leave the weapon in the root of the scene and only move its position/rotation.
            grabInteractable.retainTransformParent = true;

            // Track when the weapon is picked up / dropped
            grabInteractable.selectEntered.AddListener(OnWeaponGrabbed);
            grabInteractable.selectExited.AddListener(OnWeaponDropped);

            // For full-auto: track trigger held state
            grabInteractable.activated.AddListener(OnTriggerDown);
            grabInteractable.deactivated.AddListener(OnTriggerUp);
        }

        if (magazineSocket != null)
        {
            magazineSocket.selectEntered.AddListener(OnMagazineInserted);
            magazineSocket.selectExited.AddListener(OnMagazineRemoved);
            
            // Fix: Disable the annoying red ghost meshes when you hold the wrong object near the mag socket!
            magazineSocket.interactableCantHoverMeshMaterial = null;
        }

        if (triggerTransform != null)
        {
            triggerOriginalRotation = triggerTransform.localRotation;
        }
    }

    private void SetSubInteractablesState(bool state)
    {
        Debug.LogWarning($"[WeaponController] SetSubInteractablesState called on {gameObject.name}. State: {state}, isHeld: {isHeld}, syncedIsHeld: {syncedIsHeld.Value}");
        if (subInteractablesGroup != null)
        {
            var interactables = subInteractablesGroup.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true);
            foreach (var interactable in interactables)
            {
                // SAFETY: Never disable the main handle of the gun!
                if (interactable == grabInteractable) continue;
                
                // CRITICAL SAFETY: Never disable the XRSocketInteractor! If you disable a socket, it forcefully drops whatever is inside it!
                if (interactable is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) continue;
                
                interactable.enabled = state;
            }
        }

        // Disable the magazine's colliders so the player cannot accidentally grab it when picking up the gun
        if (currentMagazine != null)
        {
            var cols = currentMagazine.GetComponentsInChildren<Collider>();
            foreach (var col in cols)
            {
                col.enabled = state;
            }
        }
        else
        {
            Debug.LogWarning($"[WeaponController] SetSubInteractablesState - currentMagazine IS NULL! Cannot lock magazine.");
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnWeaponGrabbed);
            grabInteractable.selectExited.RemoveListener(OnWeaponDropped);
            grabInteractable.activated.RemoveListener(OnTriggerDown);
            grabInteractable.deactivated.RemoveListener(OnTriggerUp);
        }

        if (magazineSocket != null)
        {
            magazineSocket.selectEntered.RemoveListener(OnMagazineInserted);
            magazineSocket.selectExited.RemoveListener(OnMagazineRemoved);
        }
    }

    private void OnWeaponGrabbed(SelectEnterEventArgs args)
    {
        IXRSelectInteractor interactor = args.interactorObject;

        // Only activate sub-interactables if grabbed by a HAND (Direct/Ray interactor), NOT a Socket!
        if (!(interactor is XRSocketInteractor))
        {
            isHeld = true;
            if (IsOwner) syncedIsHeld.Value = true;
            
            SetSubInteractablesState(true);

            // FIX: If the weapon's Select Mode is "Multiple", the socket will try to share the weapon with the hand instead of letting it go!
            var interactorsSelecting = grabInteractable.interactorsSelecting;
            for (int i = interactorsSelecting.Count - 1; i >= 0; i--)
            {
                if (interactorsSelecting[i] is XRSocketInteractor socket)
                {
                    StartCoroutine(ForceSocketReleaseRoutine(socket, args.manager));
                }
            }

            // GUARANTEE PHYSICS ARE ACTIVE:
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }

            // --- NEW: Smart Ammo Pouch Logic (Multiplayer Safe) ---
            // Find the exact Ammo Pouch attached to the specific player who grabbed the gun
            if (!IsSpawned || IsOwner)
            {
                if (magazinePrefab != null)
                {
                    AmmoPouch localPouch = args.interactorObject.transform.root.GetComponentInChildren<AmmoPouch>();
                    
                    if (localPouch != null)
                    {
                        localPouch.SetMagazinePrefab(magazinePrefab);
                    }
                    else
                    {
                        Debug.LogWarning($"<color=yellow>[WeaponController]</color> {gameObject.name} grabbed, but no AmmoPouch was found on the player's rig ({args.interactorObject.transform.root.name})!");
                    }
                }
                else
                {
                    Debug.LogWarning($"<color=red>[WeaponController]</color> {gameObject.name} grabbed, but its Magazine Prefab is MISSING in the Inspector!");
                }
            }
        }

        // Cache the interactor so we can read its analog trigger value for trigger animation
        currentHoldingInteractor = args.interactorObject as XRBaseInputInteractor;
    }

    private System.Collections.IEnumerator ForceSocketReleaseRoutine(XRSocketInteractor socket, UnityEngine.XR.Interaction.Toolkit.XRInteractionManager manager)
    {
        // Wait 1 frame to completely escape the Interaction Manager's event lock!
        yield return new WaitForEndOfFrame();
        
        if (socket != null && grabInteractable != null)
        {
            // Explicitly force the socket to drop the weapon now that the event loop is safe
            manager.SelectCancel((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)socket, (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
            
            // Turn the socket completely off so it doesn't instantly snatch the weapon right back!
            socket.socketActive = false;
        }

        // Wait for 1.5 seconds so the player has time to pull the weapon away
        yield return new WaitForSeconds(1.5f);
        
        // Turn it back on so it can catch weapons again
        if (socket != null)
        {
            socket.socketActive = true;
        }
    }

    private void OnWeaponDropped(SelectExitEventArgs args)
    {
        isHeld = false;
        if (IsOwner) syncedIsHeld.Value = false;
        
        currentHoldingInteractor = null;

        SetSubInteractablesState(false);

        // Ensure physics are completely restored so the weapon falls to the ground when dropped!
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

        // --- NEW: Smart Ammo Pouch Logic for Dropping ---
        AmmoPouch localPouch = args.interactorObject.transform.root.GetComponentInChildren<AmmoPouch>();
        if (localPouch != null)
        {
            var interactors = args.interactorObject.transform.root.GetComponentsInChildren<XRBaseInputInteractor>();
            foreach (var interactor in interactors)
            {
                if (interactor.hasSelection)
                {
                    var grabbedObj = interactor.interactablesSelected[0].transform.gameObject;
                    
                    WeaponController wc = grabbedObj.GetComponent<WeaponController>();
                    if (wc != null && wc != this && wc.magazinePrefab != null)
                    {
                        localPouch.SetMagazinePrefab(wc.magazinePrefab);
                        break;
                    }
                    
                    ShotgunController sc = grabbedObj.GetComponent<ShotgunController>();
                    if (sc != null && sc.gameObject != this.gameObject && sc.magazinePrefab != null)
                    {
                        localPouch.SetMagazinePrefab(sc.magazinePrefab);
                        break;
                    }
                }
            }
        }
    }

    private void OnMagazineInserted(SelectEnterEventArgs args)
    {
        Magazine mag = args.interactableObject.transform.GetComponent<Magazine>();
        if (mag != null)
        {
            currentMagazine = mag;
            
            // Sync to all clients so they know this magazine is inside this gun
            NetworkObject netObj = mag.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (IsServer)
                {
                    syncedMagazine.Value = new NetworkObjectReference(netObj);
                }
                else if (IsOwner)
                {
                    SetSyncedMagazineServerRpc(new NetworkObjectReference(netObj));
                }
            }
            
            // CRITICAL FIX: The magazine just spawned into the socket. If the weapon is holstered, we need to lock it immediately!
            SetSubInteractablesState(isHeld || syncedIsHeld.Value);
        }
    }

    private void OnMagazineRemoved(SelectExitEventArgs args)
    {
        currentMagazine = null;
        if (IsServer)
        {
            syncedMagazine.Value = new NetworkObjectReference(); // Empty reference
        }
        else if (IsOwner)
        {
            SetSyncedMagazineServerRpc(new NetworkObjectReference());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetSyncedMagazineServerRpc(NetworkObjectReference magRef, ServerRpcParams rpcParams = default)
    {
        Debug.LogWarning($"<color=red>SetSyncedMagazineServerRpc called by {rpcParams.Receive.SenderClientId}!</color>");
        syncedMagazine.Value = magRef;
    }

    private void OnTriggerDown(ActivateEventArgs args)
    {
        isTriggerHeld = true;
        hasPlayedDryFire = false;
        HandleTriggerPulled();
    }

    private void HandleTriggerPulled()
    {
        if (!fullAuto)
        {
            // Single shot logic
            if (fireCooldownTimer <= 0f)
            {
                FireWeapon();
            }
        }
        else
        {
            // Full auto handled in Update, but trigger initiates it
            if (fireCooldownTimer <= 0f)
            {
                fireCooldownTimer = 60f / fireRate;
                FireWeapon();
            }
        }
    }

    private void OnTriggerUp(DeactivateEventArgs args)
    {
        isTriggerHeld = false;
        consecutiveShots = 0; // Reset burst counter when trigger is released
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.LogWarning($"[WeaponController] OnNetworkSpawn called. IsServer: {IsServer}, IsOwner: {IsOwner}, OwnerClientId: {OwnerClientId}");
        
        syncedMagazine.OnValueChanged += OnSyncedMagazineChanged;
        syncedIsHeld.OnValueChanged += OnSyncedIsHeldChanged;

        // Force a manual sync on spawn in case the Client is joining late or the value was already set!
        if (syncedMagazine.Value.NetworkObjectId != 0)
        {
            Debug.LogWarning($"[WeaponController] syncedMagazine already has a value on spawn: {syncedMagazine.Value.NetworkObjectId}. Syncing immediately.");
            StartCoroutine(SyncMagazineRoutine(syncedMagazine.Value));
        }
        if (syncedIsHeld.Value)
        {
            SetSubInteractablesState(true);
        }

        // Spawn initial magazine on the Server (for Client-Server) OR the Owner (for Distributed Authority)
        if ((IsServer || IsOwner) && spawnWithMagazine && magazinePrefab != null && magazineSocket != null)
        {
            StartCoroutine(SpawnInitialMagazineRoutine());
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        syncedMagazine.OnValueChanged -= OnSyncedMagazineChanged;
        syncedIsHeld.OnValueChanged -= OnSyncedIsHeldChanged;
    }
    
    private void OnSyncedMagazineChanged(NetworkObjectReference oldMag, NetworkObjectReference newMag)
    {
        StartCoroutine(SyncMagazineRoutine(newMag));
    }

    private IEnumerator SyncMagazineRoutine(NetworkObjectReference newMag)
    {
        Debug.LogWarning($"[WeaponController] SyncMagazineRoutine started on client. Waiting for TryGet on NetObj ID...");
        NetworkObject netObj = null;
        float timeout = 3f; // Wait up to 3 seconds for the network object to spawn on this client
        
        while (timeout > 0)
        {
            if (newMag.TryGet(out netObj))
            {
                Debug.LogWarning($"[WeaponController] TryGet SUCCESS! Magazine found on client.");
                break;
            }
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (netObj != null)
        {
            currentMagazine = netObj.GetComponent<Magazine>();
            Debug.LogWarning($"[WeaponController] currentMagazine set on client: {currentMagazine.gameObject.name}");
            
            // CRITICAL FIX: ALL Clients MUST locally socket the magazine!
            // Because XR Sockets are purely local, if the client doesn't explicitly put it in the socket, 
            // the physics engine will pull it down and it will fall out of the world!
            if (!IsServer)
            {
                var grabInteractable = currentMagazine.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grabInteractable != null && magazineSocket != null)
                {
                    Debug.LogWarning($"[WeaponController] Attempting to socket magazine on client...");
                    // Force the interaction manager to register it if necessary
                    var manager = magazineSocket.interactionManager;
                    if (manager == null)
                    {
                        manager = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
                        magazineSocket.interactionManager = manager;
                    }

                    if (manager != null)
                    {
                        if (grabInteractable.interactionManager != manager)
                        {
                            grabInteractable.interactionManager = manager;
                            manager.RegisterInteractable((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable)grabInteractable);
                        }
                        
                        if (!magazineSocket.hasSelection)
                        {
                            manager.SelectEnter(
                                (UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)magazineSocket, 
                                (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable
                            );
                            
                            // BRUTE FORCE FALLBACK: If XRI refused to socket it (e.g. mismatched layers), forcefully snap it!
                            if (!magazineSocket.hasSelection)
                            {
                                currentMagazine.transform.position = magazineSocket.attachTransform != null ? magazineSocket.attachTransform.position : magazineSocket.transform.position;
                                currentMagazine.transform.rotation = magazineSocket.attachTransform != null ? magazineSocket.attachTransform.rotation : magazineSocket.transform.rotation;
                                var rb = currentMagazine.GetComponent<Rigidbody>();
                                if (rb != null) rb.isKinematic = true;
                            }
                        }
                    }
                }
            }

            SetSubInteractablesState(isHeld || syncedIsHeld.Value); // Refresh the colliders!
        }
        else
        {
            // Only set to null if it actually failed to find it after timeout, or if it was intentionally emptied
            if (newMag.NetworkObjectId == 0)
            {
                currentMagazine = null;
            }
        }
    }

    private void OnSyncedIsHeldChanged(bool oldVal, bool newVal)
    {
        if (!IsOwner)
        {
            SetSubInteractablesState(newVal);
        }
    }

    private IEnumerator SpawnInitialMagazineRoutine()
    {
        Debug.LogWarning($"[WeaponController] SpawnInitialMagazineRoutine started. IsServer: {IsServer}, IsOwner: {IsOwner}");
        // Wait briefly for XRI and Network to stabilize
        yield return new WaitForSeconds(0.3f);

        if (currentMagazine != null || magazineSocket.hasSelection)
        {
            Debug.LogWarning($"[WeaponController] ABORTED: currentMagazine != null ({currentMagazine != null}) OR magazineSocket.hasSelection ({magazineSocket.hasSelection})");
            yield break;
        }

        Debug.LogWarning($"[WeaponController] Instantiating magazine prefab locally...");
        GameObject newMag = Instantiate(magazinePrefab, magazineSocket.transform.position, magazineSocket.transform.rotation);
        
        NetworkObject netObj = newMag.GetComponent<NetworkObject>();
        bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        
        if (!isOffline && netObj != null)
        {
            Debug.LogWarning($"[WeaponController] Spawning magazine on network with OwnerClientId: {OwnerClientId}...");
            var netConfig = Unity.Netcode.NetworkManager.Singleton.NetworkConfig;
            if (netConfig != null && !netConfig.Prefabs.Contains(magazinePrefab))
            {
                Debug.LogWarning($"[WeaponController] Forcefully adding prefab to NetworkManager...");
                netConfig.Prefabs.Add(new Unity.Netcode.NetworkPrefab { Prefab = magazinePrefab });
            }

            try
            {
                netObj.SpawnWithOwnership(OwnerClientId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WeaponController] Client failed to spawn magazine (probably Client-Server mode). Destroying local clone. Error: {e.Message}");
                Destroy(newMag);
                yield break;
            }

            yield return new WaitForEndOfFrame(); // Wait for network sync
        }
        else
        {
            Debug.LogWarning($"[WeaponController] Network is offline OR netObj is null. isOffline: {isOffline}, netObj null: {netObj == null}");
        }

        // Only proceed to socket if the magazine wasn't destroyed
        if (newMag == null) yield break;

        Debug.LogWarning($"[WeaponController] Attempting to socket magazine...");

        var grabInteractable = newMag.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grabInteractable != null)
        {
            // Lock it into the socket on the server
            if (grabInteractable.interactionManager != magazineSocket.interactionManager)
            {
                grabInteractable.interactionManager = magazineSocket.interactionManager;
                magazineSocket.interactionManager.RegisterInteractable((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable)grabInteractable);
            }
            
            magazineSocket.interactionManager.SelectEnter(
                (UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)magazineSocket, 
                (UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable
            );
            
            // BRUTE FORCE FALLBACK: If XRI refused to socket it (e.g. mismatched layers), forcefully snap it!
            if (!magazineSocket.hasSelection)
            {
                newMag.transform.position = magazineSocket.attachTransform != null ? magazineSocket.attachTransform.position : magazineSocket.transform.position;
                newMag.transform.rotation = magazineSocket.attachTransform != null ? magazineSocket.attachTransform.rotation : magazineSocket.transform.rotation;
                var rb = newMag.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
        }
    }

    private void Start()
    {
        // If testing offline, spawn it immediately
        if (spawnWithMagazine && magazinePrefab != null && magazineSocket != null)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                StartCoroutine(SpawnInitialMagazineRoutine());
            }
        }

        // Safety check to ensure magazine is registered if it spawned attached to the socket (e.g. via Editor)
        if (magazineSocket != null && currentMagazine == null)
        {
            UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable attachedObj = magazineSocket.firstInteractableSelected;
            if (attachedObj != null)
            {
                currentMagazine = attachedObj.transform.GetComponent<Magazine>();
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Store the resting position/rotation of the visual model
        if (weaponModel != null)
        {
            originalModelRotation = weaponModel.localEulerAngles;
            originalModelPosition = weaponModel.localPosition;
        }

        if (boltTransform != null)
        {
            originalBoltPosition = boltTransform.localPosition;
        }

        if (shellPrefab != null && shellEjectionPoint != null)
        {
            shellPool = new Queue<GameObject>();
            activeShellCoroutines = new Dictionary<GameObject, Coroutine>();

            // Create a parent object just to keep the hierarchy clean
            Transform poolParent = new GameObject(gameObject.name + "_ShellPool").transform;

            for (int i = 0; i < shellPoolSize; i++)
            {
                GameObject shell = Instantiate(shellPrefab, poolParent);
                
                // Strip Netcode components from purely visual local shells so they don't throw errors!
                if (shell.TryGetComponent<NetworkObject>(out var netObj)) Destroy(netObj);
                if (shell.TryGetComponent<XRMultiplayer.NetworkPhysicsInteractable>(out var phys)) Destroy(phys);
                if (shell.TryGetComponent<XRMultiplayer.ClientNetworkTransform>(out var cnt)) Destroy(cnt);

                shell.SetActive(false);
                shellPool.Enqueue(shell);
            }
        }

        // Ensure sub-interactables and magazine are properly disabled now that everything has initialized
        SetSubInteractablesState(false);
    }

    private void TriggerSlideRecoil()
    {
        if (shellPrefab != null && shellEjectionPoint != null)
        {
            // Start physical bolt slide animation
            targetBoltOffset = boltTravelDistance;

            // Spawn an ejected shell immediately
            GameObject shellParent = GameObject.Find("ShellPool");
            Transform poolParent = shellParent != null ? shellParent.transform : null;
            
            if (shellPool != null && shellPool.Count > 0)
            {
                GameObject shell = shellPool.Dequeue();
                shellPool.Enqueue(shell);
                shell.transform.position = shellEjectionPoint.position;
                shell.transform.rotation = shellEjectionPoint.rotation;
                EjectShellPhysics(shell);
            }
            else
            {
                GameObject shell = Instantiate(shellPrefab, poolParent);
                shell.transform.position = shellEjectionPoint.position;
                shell.transform.rotation = shellEjectionPoint.rotation;
                EjectShellPhysics(shell);
            }
            hasEjectedShell = true;
        }
    }

    private void Update()
    {
        // Handle fire rate cooldown timer
        if (fireCooldownTimer > 0) fireCooldownTimer -= Time.deltaTime;

        // Auto-firing logic for when the trigger is held down over multiple frames
        bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
        if (IsOwner || isOffline)
        {
            if (fullAuto && isTriggerHeld && isHeld)
            {
                if (fireCooldownTimer <= 0f)
                {
                    fireCooldownTimer = 60f / fireRate; // Convert RPM to seconds between shots
                    FireWeapon();
                }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
        {
            DebugRackSlide();
        }
#endif

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

        // ── Procedural Recoil (Spring Math) ──
        if (weaponModel != null)
        {
            targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, Time.deltaTime * returnSpeed);
            targetPosition = Vector3.Lerp(targetPosition, Vector3.zero, Time.deltaTime * returnSpeed);

            currentRotation = Vector3.Slerp(currentRotation, targetRotation, Time.deltaTime * snappiness);
            currentPosition = Vector3.Lerp(currentPosition, targetPosition, Time.deltaTime * snappiness);

            weaponModel.localRotation = Quaternion.Euler(originalModelRotation + currentRotation);
            weaponModel.localPosition = originalModelPosition + currentPosition;
        }

        if (boltTransform != null)
        {
            if (!isSlideGrabbed)
            {
                float targetOffset = isSlideLockedBack.Value ? slideLockDistance : 0f;
                targetBoltOffset = Mathf.Lerp(targetBoltOffset, targetOffset, Time.deltaTime * boltReturnSpeed);
                currentBoltOffset = Mathf.Lerp(currentBoltOffset, targetBoltOffset, Time.deltaTime * boltSnappiness);
            }

            // For a correct prefab (+Z forward), moving backwards requires a negative local Z offset
            boltTransform.localPosition = originalBoltPosition + new Vector3(0, 0, -currentBoltOffset);

            // Eject shell when the bolt visibly moves enough (lowered threshold to 10% to guarantee it triggers even at high return speeds)
            if (!hasEjectedShell && Mathf.Abs(currentBoltOffset) > Mathf.Abs(boltTravelDistance) * 0.1f)
            {
                EjectShell();
                hasEjectedShell = true;
            }
        }
    }

    // Call this function when the VR player pulls the trigger
    public void FireWeapon()
    {
        if (IsSpawned && !IsOwner) return; // Only owner calculates hits and ammo

        if (ShootingRangeManager.Instance != null && !ShootingRangeManager.Instance.isShootingAllowed)
        {
            if (!hasPlayedDryFire)
            {
                if (IsSpawned) DryFireRpc();
                else PlayDryFireLocal();
                
                hasPlayedDryFire = true;
            }
            return;
        }

        if (barrelPoint == null)
        {
            Debug.LogWarning("WeaponController: No barrel point assigned! Please create an empty GameObject at the barrel tip and assign it.");
            return;
        }

        if (!isChambered.Value)
        {
            // Dry fire
            if (!hasPlayedDryFire)
            {
                if (IsSpawned) DryFireRpc();
                else PlayDryFireLocal();
                
                hasPlayedDryFire = true;
            }
            return;
        }

        // We have a chambered round, commit to firing!
        isChambered.Value = false; // Spend the chambered round
        consecutiveShots++;

        // Report to the Training Grounds Manager that 1 projectile was fired
        OnProjectilesFired?.Invoke(1);

        // After firing, the semi-auto gun attempts to chamber a new round
        if (currentMagazine != null && currentMagazine.HasAmmo())
        {
            currentMagazine.ConsumeAmmo();
            isChambered.Value = true;
        }
        else if (hasSlideLockOnEmpty)
        {
            isSlideLockedBack.Value = true;
        }

        // 3. Logic: Raycast Hit Detection (Using NonAlloc to pierce triggers for Headshots)
        bool hitSomething = false;
        Vector3 hitPoint = Vector3.zero;
        Vector3 hitNormal = Vector3.zero;
        SurfaceType hitType = SurfaceType.Default;

        // VISUAL DEBUG: Draw a 5-second red line in the Scene view so the user can see EXACTLY where the raycast went!
        Debug.DrawRay(barrelPoint.position, barrelPoint.forward * range, Color.red, 5f);

        RaycastHit[] hitBuffer = new RaycastHit[20];
        int hitCount = Physics.RaycastNonAlloc(barrelPoint.position, barrelPoint.forward, hitBuffer, range, hitMask, QueryTriggerInteraction.Collide);

        RaycastHit closestSolidHit = new RaycastHit();
        float closestSolidDist = float.MaxValue;
        HittableSurface closestHittable = null;

        // Keep track of damage zones so we can apply damage AFTER the decal is spawned
        System.Collections.Generic.List<TargetDamageZone> zonesToDamage = new System.Collections.Generic.List<TargetDamageZone>();
        System.Collections.Generic.List<AnimalDamageZone> animalZonesToDamage = new System.Collections.Generic.List<AnimalDamageZone>();

        // Sort hits by distance to correctly calculate bullet penetration
        for (int i = 0; i < hitCount - 1; i++) {
            for (int j = i + 1; j < hitCount; j++) {
                if (hitBuffer[i].distance > hitBuffer[j].distance) {
                    RaycastHit temp = hitBuffer[i];
                    hitBuffer[i] = hitBuffer[j];
                    hitBuffer[j] = temp;
                }
            }
        }

        float entryDistance = -1f;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit currentHit = hitBuffer[i];

            // 1. Mark our entry point into a solid object (e.g. skin, tree, wall)
            if (entryDistance < 0f && !currentHit.collider.isTrigger)
            {
                entryDistance = currentHit.distance;
            }

            // 2. Check if the bullet ran out of penetration power
            if (entryDistance >= 0f && (currentHit.distance - entryDistance) > penetrationDepth)
            {
                // The bullet stopped inside the object! It cannot hit any more organs behind this depth.
                break;
            }

            // 1a. Check if we hit a Standard Target Damage Zone
            TargetDamageZone damageZone = currentHit.collider.GetComponentInParent<TargetDamageZone>();
            if (damageZone != null)
            {
                if (!zonesToDamage.Contains(damageZone))
                    zonesToDamage.Add(damageZone);
            }
            
            // 1b. Check if we hit an Animal Damage Zone (e.g., Boar)
            AnimalDamageZone animalZone = currentHit.collider.GetComponentInParent<AnimalDamageZone>();
            if (animalZone != null)
            {
                if (!animalZonesToDamage.Contains(animalZone))
                    animalZonesToDamage.Add(animalZone);
            }
            
            // 2. Check if it's a solid surface for the Decal (Ignore triggers so bullet holes don't float)
            if (!currentHit.collider.isTrigger)
            {
                HittableSurface hittable = currentHit.collider.GetComponentInParent<HittableSurface>();

                // A bullet should stop at ANY solid collider, even if it lacks a HittableSurface script!
                if (currentHit.distance < closestSolidDist)
                {
                    closestSolidDist = currentHit.distance;
                    closestSolidHit = currentHit;
                    closestHittable = hittable;
                }
            }
        }

        // If we found a solid surface, prepare to spawn the decal and trigger legacy hit events
        if (closestSolidDist != float.MaxValue)
        {
            hitSomething = true;
            hitPoint = closestSolidHit.point;
            hitNormal = closestSolidHit.normal;
            
            // Bulletproof check for Unity painted Terrain Trees: 
            // Unity bakes tree colliders as CapsuleColliders directly onto the Terrain GameObject!
            bool isPaintedTerrainTree = (closestSolidHit.collider is CapsuleCollider) && 
                                        (closestSolidHit.collider.gameObject.GetComponent<TerrainCollider>() != null);

            // Check for Physics Material override
            bool hasWoodPhysicsMaterial = (closestSolidHit.collider.sharedMaterial != null && 
                                           closestSolidHit.collider.sharedMaterial.name.ToLower().Contains("wood"));

            if (hasWoodPhysicsMaterial || isPaintedTerrainTree)
            {
                hitType = SurfaceType.Wood;
            }
            else if (closestSolidHit.collider is TerrainCollider tCol)
            {
                // Unity absorbs tree colliders into the TerrainCollider!
                // We can mathematically prove if we hit a tree by checking if the hit location is above the ground.
                Terrain terrain = tCol.GetComponent<Terrain>();
                if (terrain != null)
                {
                    float groundHeight = terrain.SampleHeight(closestSolidHit.point) + terrain.transform.position.y;
                    
                    // If the bullet hit something sticking at least 0.4 meters out of the ground, it's a tree trunk!
                    if (closestSolidHit.point.y > groundHeight + 0.4f)
                    {
                        hitType = SurfaceType.Wood;
                    }
                    else
                    {
                        hitType = closestHittable != null ? closestHittable.surfaceType : SurfaceType.Dirt;
                    }
                }
                else
                {
                    hitType = closestHittable != null ? closestHittable.surfaceType : SurfaceType.Dirt;
                }
            }
            else
            {
                hitType = closestHittable != null ? closestHittable.surfaceType : SurfaceType.Default;
            }
            
            Debug.Log($"<color=yellow>[Weapon Hit]</color> Hit Collider: <b>{closestSolidHit.collider.name}</b> on GameObject: <b>{closestSolidHit.collider.gameObject.name}</b>. " +
                      $"PhysicsMat: {(closestSolidHit.collider.sharedMaterial != null ? closestSolidHit.collider.sharedMaterial.name : "None")}. " +
                      $"Final HitType: <b>{hitType}</b>");

            // Trigger the old generic hit event
            if (closestHittable != null) closestHittable.OnHit(closestSolidHit);

            // Spawn hit effect locally with parent for the shooter
            if (HitEffectPoolManager.Instance != null)
            {
                HitEffectPoolManager.Instance.SpawnHitEffect(hitPoint, hitNormal, hitType, closestSolidHit.collider.transform);
            }
        }

        // Tell all clients to play visuals
        if (IsSpawned)
            FireVisualsRpc(consecutiveShots);
        else
            PlayFireVisualsLocal(consecutiveShots);
        
        if (hitSomething && IsSpawned)
        {
            SpawnHitEffectRpc(hitPoint, hitNormal, hitType);
        }

        // 4. NOW apply the damage (so the target doesn't rotate BEFORE the decal is parented!)
        foreach (var zone in zonesToDamage)
        {
            zone.ApplyDamage(weaponDamage);
        }

        foreach (var animalZone in animalZonesToDamage)
        {
            animalZone.ApplyDamage(weaponDamage);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void DryFireRpc()
    {
        PlayDryFireLocal();
    }

    private void PlayDryFireLocal()
    {
        PlayDryFireAudio();
    }

    private void PlayDryFireAudio()
    {
        if (audioSource != null && dryFireSound != null)
        {
            audioSource.PlayOneShot(dryFireSound, shootVolume);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // EFFECTS & RECOIL
    // ────────────────────────────────────────────────────────────────────────

    private void PlayShootEffects()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.SetActive(false);
            muzzleFlash.SetActive(true);
            ParticleSystem[] pSystems = muzzleFlash.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem p in pSystems)
            {
                p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                p.Play(true);
            }
        }

        if (audioSource != null && shootSound != null)
        {
            audioSource.pitch = Random.Range(soundPitchRange.x, soundPitchRange.y);
            audioSource.PlayOneShot(shootSound, shootVolume);
        }

        // Haptics
        if (currentHoldingInteractor != null)
        {
            var inputInteractor = currentHoldingInteractor as XRBaseInputInteractor;
            if (inputInteractor != null)
            {
                inputInteractor.SendHapticImpulse(hapticIntensity, hapticDuration);
            }
        }
    }

    [Rpc(SendTo.NotOwner)]
    private void SpawnHitEffectRpc(Vector3 hitPoint, Vector3 hitNormal, SurfaceType hitType)
    {
        SpawnHitEffectLocal(hitPoint, hitNormal, hitType);
    }

    private void SpawnHitEffectLocal(Vector3 hitPoint, Vector3 hitNormal, SurfaceType hitType)
    {
        if (HitEffectPoolManager.Instance != null)
        {
            // Spawn without parent on non-owner/offline clients
            HitEffectPoolManager.Instance.SpawnHitEffect(hitPoint, hitNormal, hitType, null);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void FireVisualsRpc(int shots)
    {
        PlayFireVisualsLocal(shots);
    }

    private void PlayFireVisualsLocal(int shots)
    {
        PlayShootEffects();

        ApplyProceduralRecoil();

        if (boltTransform != null)
        {
            targetBoltOffset = boltTravelDistance;
            currentBoltOffset = boltTravelDistance; // Snap the target offset back instantly to guarantee full visual travel
            hasEjectedShell = false; // Prepare a new shell to be ejected as the bolt travels back
        }
        else
        {
            EjectShell(); // Instantly eject if the gun doesn't have an animated bolt!
        }
    }

    private void ApplyProceduralRecoil()
    {
        if (weaponModel != null)
        {
            RecoilTier tier;
            
            if (consecutiveShots <= 3)
                tier = tier1_Shots1to3;
            else if (consecutiveShots <= 8)
                tier = tier2_Shots4to8;
            else if (consecutiveShots <= 17)
                tier = tier3_Shots9to17;
            else
                tier = tier4_Shots18plus;

            float pitch = Random.Range(tier.pitchRange.x, tier.pitchRange.y);
            float yaw = Random.Range(tier.yawRange.x, tier.yawRange.y);
            float roll = Random.Range(tier.rollRange.x, tier.rollRange.y);
            float kick = tier.backwardKick;

            if (grabInteractable != null && grabInteractable.IsTwoHandedGrabbed)
            {
                pitch *= twoHandRecoilModifier;
                yaw *= twoHandRecoilModifier;
                roll *= twoHandRecoilModifier;
                kick *= twoHandRecoilModifier;
            }

            targetRotation += new Vector3(-pitch, yaw, roll);
            targetPosition -= new Vector3(0, 0, kick);
        }
    }

    private void EjectShell()
    {
        if (shellEjectionPoint == null || shellPrefab == null || shellPool == null || shellPool.Count == 0) return;

        // Dequeue oldest shell (cyclic buffer format)
        GameObject shell = shellPool.Dequeue();

        // Move the shell to the ejection port BEFORE applying physics!
        shell.transform.position = shellEjectionPoint.position;
        shell.transform.rotation = shellEjectionPoint.rotation * Quaternion.Euler(shellSpawnRotationOffset);

        EjectShellPhysics(shell);

        // Requeue to end
        shellPool.Enqueue(shell);
    }

    private void EjectShellPhysics(GameObject shell)
    {
        if (shellEjectionPoint == null || shellPrefab == null || shellPool == null || shellPool.Count == 0) return;

        shell.SetActive(true);

        Rigidbody shellRb = shell.GetComponent<Rigidbody>();
        if (shellRb != null)
        {
            shellRb.linearVelocity = Vector3.zero;
            shellRb.angularVelocity = Vector3.zero;

            Vector3 ejectDirection = shellEjectionPoint.right + (shellEjectionPoint.up * Random.Range(0.2f, 0.5f));
            
            shellRb.AddForce(ejectDirection.normalized * shellEjectionForce, ForceMode.Impulse);

            shellRb.AddTorque(new Vector3(Random.Range(-shellTorque, shellTorque), Random.Range(-shellTorque, shellTorque), Random.Range(-shellTorque, shellTorque)), ForceMode.Impulse);
        }

        if (activeShellCoroutines.TryGetValue(shell, out Coroutine existingCoroutine))
        {
            if (existingCoroutine != null) StopCoroutine(existingCoroutine);
            activeShellCoroutines.Remove(shell);
        }

        Coroutine newCoroutine = StartCoroutine(ReturnShellToPool(shell, shellLifeTime));
        activeShellCoroutines.Add(shell, newCoroutine);
    }

    private IEnumerator ReturnShellToPool(GameObject shell, float time)
    {
        yield return new WaitForSeconds(time);
        if (shell != null)
        {
            shell.SetActive(false);
        }
    }

    // Editor Helper Method: Allows you to right-click the script in Unity Editor -> "Test Fire Gun"
    // so you can test shooting mechanics without wearing the VR headset!
    [ContextMenu("Test Fire Gun")]
    public void DebugFire()
    {
        // For debug firing, force chamber a round first so it always works
        isChambered.Value = true;
        FireWeapon();
        Debug.Log("Debug Fire Triggered!");
    }

    [ContextMenu("Debug Rack Slide")]
    public void DebugRackSlide()
    {
        RackSlide();
        Debug.Log("Debug Rack Triggered!");
    }

    public void RackSlide()
    {
        if (IsSpawned && !IsOwner) return; // Only owner initiates mechanical actions
        
        // Release the slide lock if it was locked back
        isSlideLockedBack.Value = false;
        
        bool ejectRound = false;
        
        // 1. If there's an unfired chambered round, eject it. 
        if (isChambered.Value)
        {
            ejectRound = true;
            isChambered.Value = false;
        }

        // 2. Chamber a new round from the magazine if available
        if (currentMagazine != null)
        {
            if (currentMagazine.HasAmmo())
            {
                currentMagazine.ConsumeAmmo();
                isChambered.Value = true;
                Debug.Log("Round Chambered!");
            }
            else
            {
                Debug.LogWarning("Magazine is inserted, but it is empty!");
            }
        }
        else
        {
            Debug.LogWarning("Could not chamber round: No Magazine detected in the Socket!");
        }

        if (IsSpawned)
            RackSlideRpc(ejectRound);
        else
            PlayRackSlideLocal(ejectRound);
    }

    [Rpc(SendTo.Everyone)]
    private void RackSlideRpc(bool ejectRound)
    {
        PlayRackSlideLocal(ejectRound);
    }

    private void PlayRackSlideLocal(bool ejectRound)
    {
        if (ejectRound)
        {
            EjectShell();
        }

        // Visually rack the bolt via the procedural animation back to simulate the rack action
        if (boltTransform != null)
        {
            targetBoltOffset = boltTravelDistance;
            currentBoltOffset = boltTravelDistance; // Snap it back instantly so you can see it return
            // We set this to true so we don't accidentally double-eject a shell through the Update logic
            hasEjectedShell = true;
        }
    }
}
