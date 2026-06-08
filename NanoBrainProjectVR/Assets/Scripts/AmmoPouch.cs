using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
public class AmmoPouch : MonoBehaviour
{
    // Singleton so weapons ALWAYS find the active, real pouch instead of hidden duplicates!
    public static AmmoPouch Instance;

    [Tooltip("The Magazine prefab that this pouch will currently dispense. This is updated dynamically by the WeaponController!")]
    public GameObject magazinePrefab;
    
    private GameObject currentSpawnedMag;
    private IXRSelectInteractable currentMagInteractable;

    private void Awake()
    {
        // Enforce singleton so guns never accidentally talk to a hidden duplicate pouch
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple Ammo Pouches detected! Destroying the duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Called dynamically by ShotgunController/WeaponController whenever you grab a new weapon!
    public void SetMagazinePrefab(GameObject newMagazinePrefab)
    {
        if (newMagazinePrefab != null)
        {
            magazinePrefab = newMagazinePrefab;
            Debug.Log($"<color=cyan>[AmmoPouch]</color> Updated to dispense: {newMagazinePrefab.name}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering the pouch is a VR Hand
        XRDirectInteractor hand = other.GetComponentInParent<XRDirectInteractor>();
        
        if (hand != null && magazinePrefab != null && currentSpawnedMag == null)
        {
            // Spawn a fresh magazine right inside the pouch, exactly where the hand is!
            currentSpawnedMag = Instantiate(magazinePrefab, transform.position, transform.rotation);
            currentMagInteractable = currentSpawnedMag.GetComponentInChildren<IXRSelectInteractable>();

            // Listen for when the player actually grabs it
            if (currentMagInteractable != null)
            {
                currentMagInteractable.selectEntered.AddListener(OnMagazineGrabbed);
                Debug.Log($"<color=cyan>[AmmoPouch]</color> Spawned invisible {magazinePrefab.name} for the hand to natively grab!");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        XRDirectInteractor hand = other.GetComponentInParent<XRDirectInteractor>();
        
        if (hand != null && currentSpawnedMag != null)
        {
            // If the hand leaves the pouch, but didn't grab the magazine, destroy it to keep the vest clean!
            if (currentMagInteractable != null && !currentMagInteractable.isSelected)
            {
                currentMagInteractable.selectEntered.RemoveListener(OnMagazineGrabbed);
                Destroy(currentSpawnedMag);
                currentSpawnedMag = null;
                currentMagInteractable = null;
            }
        }
    }

    private void OnMagazineGrabbed(SelectEnterEventArgs args)
    {
        // The player grabbed the magazine out of the pouch!
        // We clean up our reference so the pouch is ready to spawn a new one next time.
        if (currentMagInteractable != null)
        {
            currentMagInteractable.selectEntered.RemoveListener(OnMagazineGrabbed);
        }

        currentSpawnedMag = null;
        currentMagInteractable = null;
    }
}
