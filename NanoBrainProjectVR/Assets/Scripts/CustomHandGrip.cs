using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; 

[RequireComponent(typeof(XRGrabInteractable))]
public class CustomHandGrip : MonoBehaviour
{
    [Tooltip("What animation Pose number should be forced when this object is grabbed? (e.g. 0 = Normal, 1 = Gun Grip, 2 = Magazine Grip)")]
    public int poseID = 1;

    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrab);
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        // Find the Animator dynamically on whatever hand just grabbed us!
        Animator handAnim = args.interactorObject.transform.GetComponentInChildren<Animator>();
        if(handAnim != null)
        {
            handAnim.SetInteger("Pose", poseID);
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        Animator handAnim = args.interactorObject.transform.GetComponentInChildren<Animator>();
        if (handAnim != null)
        {
            // When dropped, reset to the default empty hand
            handAnim.SetInteger("Pose", 0); 
        }
    }
}
