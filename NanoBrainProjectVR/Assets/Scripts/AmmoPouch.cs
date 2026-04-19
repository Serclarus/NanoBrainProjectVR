using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRSocketInteractor))]
public class AmmoPouch : MonoBehaviour
{
    [Tooltip("The Magazine prefab that this pouch will constantly dispense.")]
    public GameObject magazinePrefab;
    
    [Tooltip("How long after grab before a new magazine is spawned in the pouch (prevents physics clipping glitches).")]
    public float spawnDelay = 0.5f;

    private XRSocketInteractor socket;
    private bool isSpawning = false;

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectExited.AddListener(OnMagazineRemoved);

        // Prevent the socket from auto-grabbing random mags the player drops nearby.
        // We only insert mags manually via SelectEnter in SpawnMagazine().
        socket.hoverSocketSnapping = false;
        socket.startingSelectedInteractable = null;
    }

    private void OnDestroy()
    {
        socket.selectExited.RemoveListener(OnMagazineRemoved);
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
}
