using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

using Unity.Netcode;

[RequireComponent(typeof(XRSocketInteractor))]
public class AmmoPouch : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    [Tooltip("The Magazine prefab that this pouch will constantly dispense.")]
    public GameObject magazinePrefab;
    
    [Tooltip("How long after grab before a new magazine is spawned in the pouch (prevents physics clipping glitches).")]
    public float spawnDelay = 0.5f;

    private XRSocketInteractor socket;
    private bool isSpawning = false;

    public bool canProcess => isActiveAndEnabled;

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectExited.AddListener(OnMagazineRemoved);

        // Prevent the socket from auto-grabbing random mags the player drops nearby.
        // We only insert mags manually via SelectEnter in SpawnMagazine().
        socket.hoverSocketSnapping = false;
        socket.startingSelectedInteractable = null;

        // Add filters so it doesn't grab or hover items dropped in its trigger
        socket.selectFilters.Add(this);
        socket.hoverFilters.Add(this);
    }

    private void OnDestroy()
    {
        socket.selectExited.RemoveListener(OnMagazineRemoved);
        socket.selectFilters.Remove(this);
        socket.hoverFilters.Remove(this);
    }

    private void Start()
    {
        // Fill the socket on spawn if empty
        if (!socket.hasSelection)
        {
            SpawnMagazine();
        }
    }

    private void OnMagazineRemoved(SelectExitEventArgs args)
    {
        // Ignore selectExited events we caused ourselves during spawning
        if (isSpawning) return;

        // Only spawn a new one if the game is still running
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(SpawnRoutine());
        }
    }

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(spawnDelay);

        if (!socket.hasSelection)
        {
            SpawnMagazine();
        }
    }

    private void SpawnMagazine()
    {
        if (magazinePrefab == null)
        {
            Debug.LogWarning("AmmoPouch: No magazine prefab assigned to spawn!");
            return;
        }

        isSpawning = true;

        if (XRINetworkPlayer.LocalPlayer != null)
        {
            // Ask the Server to spawn the magazine via our Network Avatar
            XRINetworkPlayer.LocalPlayer.SpawnMagazineServerRpc(transform.position, transform.rotation);
            
            // Wait for it to arrive from the server, then socket it locally
            StartCoroutine(WaitAndSocket());
        }
        else
        {
            // Fallback for completely offline play
            GameObject newMag = Instantiate(magazinePrefab, transform.position, transform.rotation);
            IXRSelectInteractable interactable = newMag.GetComponentInChildren<IXRSelectInteractable>();
            if (interactable != null)
            {
                socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, interactable);
            }
            isSpawning = false;
        }
    }

    private IEnumerator WaitAndSocket()
    {
        // Give the server a moment to spawn the object and sync it to us
        yield return new WaitForSeconds(0.2f);

        // Find the newest magazine that was spawned near us
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);
        IXRSelectInteractable targetMag = null;

        foreach (var hit in hits)
        {
            IXRSelectInteractable interactable = hit.GetComponentInParent<IXRSelectInteractable>();
            if (interactable != null && hit.GetComponentInParent<Magazine>() != null)
            {
                // Make sure it's not already held by someone else
                if (interactable.interactorsSelecting.Count == 0)
                {
                    targetMag = interactable;
                    break;
                }
            }
        }

        if (targetMag != null)
        {
            socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, targetMag);
        }
        
        isSpawning = false;
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        if (isSpawning) return true;
        if (socket.hasSelection && socket.interactablesSelected.Contains(interactable)) return true;
        return false;
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        if (isSpawning) return true;
        if (interactable is IXRSelectInteractable selectInteractable)
        {
            if (socket.hasSelection && socket.interactablesSelected.Contains(selectInteractable)) return true;
        }
        return false;
    }
}
