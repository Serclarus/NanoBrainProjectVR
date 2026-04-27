using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

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

        GameObject newMag = Instantiate(magazinePrefab, transform.position, transform.rotation);
        IXRSelectInteractable interactable = newMag.GetComponentInChildren<IXRSelectInteractable>();

        if (interactable != null)
        {
            socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, interactable);
        }

        isSpawning = false;
    }

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        // Only allow selection if we are explicitly spawning it right now,
        // or if it's ALREADY held by this socket.
        if (isSpawning) return true;
        if (socket.hasSelection && socket.interactablesSelected.Contains(interactable)) return true;
        
        return false;
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        // Same logic for hover - only allow what we spawn or what we already hold.
        if (isSpawning) return true;
        if (interactable is IXRSelectInteractable selectInteractable)
        {
            if (socket.hasSelection && socket.interactablesSelected.Contains(selectInteractable)) return true;
        }

        return false;
    }
}
