using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Needed for XRI 3.0+

[RequireComponent(typeof(XRGrabInteractable))]
public class Magazine : MonoBehaviour
{
    [Header("Ammo Settings")]
    [Tooltip("Maximum amount of bullets this magazine can hold.")]
    public int maxAmmo = 30;

    [Tooltip("Current amount of bullets in the magazine.")]
    public int currentAmmo = 30;

    [Tooltip("If true, the magazine will never run out of ammo.")]
    public bool infiniteAmmo = false;

    [Header("Cleanup Settings")]
    [Tooltip("How many seconds until the magazine destroys itself after being dropped empty?")]
    public float despawnDelay = 10f;

    private XRGrabInteractable grabInteractable;
    private Coroutine despawnCoroutine;
    private bool isHeld = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrabbedOrSocketed);
        grabInteractable.selectExited.AddListener(OnDropped);
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbedOrSocketed);
            grabInteractable.selectExited.RemoveListener(OnDropped);
        }
    }

    private void Start()
    {
        // Safety check to ensure we don't start with more ammo than allowed 
        // (unless set via inspector intentionally, but good practice).
        if (currentAmmo > maxAmmo)
        {
            currentAmmo = maxAmmo;
        }

        CheckDespawnCondition(); // Run check in case it spawns totally empty on the floor
    }

    private void OnGrabbedOrSocketed(SelectEnterEventArgs args)
    {
        isHeld = true;
        // Instantly cancel despawn if someone picks it up or sockets it
        if (despawnCoroutine != null)
        {
            StopCoroutine(despawnCoroutine);
            despawnCoroutine = null;
        }
    }

    private void OnDropped(SelectExitEventArgs args)
    {
        isHeld = false;
        CheckDespawnCondition();
    }

    private void CheckDespawnCondition()
    {
        if (!isHeld && !HasAmmo() && despawnCoroutine == null)
        {
            despawnCoroutine = StartCoroutine(DespawnRoutine());
        }
    }

    private IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(despawnDelay);
        // Poof!
        Destroy(gameObject);
    }

    /// <summary>
    /// Checks if there is at least one bullet left in the magazine.
    /// </summary>
    public bool HasAmmo()
    {
        if (infiniteAmmo) return true;
        return currentAmmo > 0;
    }

    /// <summary>
    /// Removes one bullet from the magazine if available.
    /// Returns true if a bullet was successfully consumed, false if empty.
    /// </summary>
    public bool ConsumeAmmo()
    {
        if (infiniteAmmo) return true;

        if (currentAmmo > 0)
        {
            currentAmmo--;
            
            // If we just shot the last bullet, check if we immediately need to start the despawn timer (if it's not held/socketed somehow)
            if (currentAmmo == 0)
            {
                CheckDespawnCondition();
            }

            return true;
        }
        return false;
    }
}
