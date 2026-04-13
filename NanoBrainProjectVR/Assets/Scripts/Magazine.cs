using UnityEngine;
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

    private void Start()
    {
        // Safety check to ensure we don't start with more ammo than allowed 
        // (unless set via inspector intentionally, but good practice).
        if (currentAmmo > maxAmmo)
        {
            currentAmmo = maxAmmo;
        }
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
            return true;
        }
        return false;
    }
}
