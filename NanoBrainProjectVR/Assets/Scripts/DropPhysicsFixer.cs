using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class DropPhysicsFixer : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        if (grabInteractable != null)
        {
            grabInteractable.selectExited.AddListener(OnDropped);
        }
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectExited.RemoveListener(OnDropped);
        }
    }

    private void OnDropped(SelectExitEventArgs args)
    {
        // Force physics back on when dropped into the world (not into a socket).
        // This fixes items floating after being pulled from the ammo pouch socket,
        // because XRI may remember the kinematic state the socket set.
        bool droppedIntoSocket = args.interactorObject is XRSocketInteractor;
        if (!droppedIntoSocket && rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }
}
