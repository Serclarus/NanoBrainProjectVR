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

    private void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.selectExited.AddListener(OnMagazineRemoved);
    }

    private void OnDestroy()
    {
        socket.selectExited.RemoveListener(OnMagazineRemoved);
    }

    private void Start()
    {
        // Try filling the socket on spawn if empty
        if (!socket.hasSelection)
        {
            SpawnMagazine();
        }
    }

    private void OnMagazineRemoved(SelectExitEventArgs args)
    {
        // Only spawn a new one if the player actually grabbed it out, not if we destroyed it
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

        GameObject newMag = Instantiate(magazinePrefab, transform.position, transform.rotation);
        IXRSelectInteractable interactable = newMag.GetComponentInChildren<IXRSelectInteractable>();

        if (interactable != null)
        {
            // By telling the InteractionManager to SelectEnter, we securely slot it into the socket perfectly.
            // Ignore warnings about obsolete APIs, this works cross-version for XRI 2 and 3 cleanly.
            socket.interactionManager.SelectEnter((IXRSelectInteractor)socket, interactable);
        }
    }
}
