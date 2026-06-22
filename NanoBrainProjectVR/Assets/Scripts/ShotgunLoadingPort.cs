using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(BoxCollider))]
public class ShotgunLoadingPort : MonoBehaviour
{
    [Tooltip("The main ShotgunController script on the weapon")]
    public ShotgunController shotgun;
    
    [Tooltip("The tag assigned to your physical Shotgun Shell prefabs")]
    public string shellTag = "Ammo";

    private void OnTriggerEnter(Collider other)
    {
        if (shotgun == null || (shotgun.IsSpawned && !shotgun.IsOwner)) return;

        // Ensure the object touching the port (or its parent) is actually a shotgun shell
        bool isShell = other.CompareTag(shellTag) || (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(shellTag));
        if (isShell)
        {
            XRGrabInteractable grabItem = other.GetComponentInParent<XRGrabInteractable>();

            // If the shell is currently held by a Socket (like the Ammo Pouch), IGNORE IT!
            // This prevents the shotgun from magically sucking ammo out of the pouch when they touch.
            if (grabItem != null && grabItem.isSelected && grabItem.firstInteractorSelecting is XRSocketInteractor)
            {
                return;
            }

            // Ensure the tube isn't already full
            if (shotgun.currentAmmo.Value < shotgun.maxAmmoCapacity)
            {
                // 1. Force the player's hand to let go of the shell to prevent XR warnings
                if (grabItem != null && grabItem.isSelected)
                {
                    grabItem.interactionManager.SelectCancel(grabItem.firstInteractorSelecting, grabItem);
                }

                // 2. Destroy the physical shell
                Destroy(other.gameObject);

                // 3. Add the digital ammo (QoL: 1 physical shell = X digital shells)
                int newAmmo = shotgun.currentAmmo.Value + shotgun.ammoPerShellReloaded;
                shotgun.currentAmmo.Value = Mathf.Min(newAmmo, shotgun.maxAmmoCapacity);

                // 4. Play a satisfying click sound
                if (shotgun.audioSource != null && shotgun.pumpForwardSound != null)
                {
                    shotgun.audioSource.PlayOneShot(shotgun.pumpForwardSound, 0.5f);
                }

                Debug.Log($"Shotgun Loaded! Current Ammo: {shotgun.currentAmmo.Value} / {shotgun.maxAmmoCapacity}");
            }
        }
    }
}
