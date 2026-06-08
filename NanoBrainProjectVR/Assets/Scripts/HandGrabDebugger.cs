using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class HandGrabDebugger : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor[] interactors;

    private void Start()
    {
        // Find ALL interactors in the ENTIRE scene!
        interactors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(true);

        foreach (var interactor in interactors)
        {
            interactor.selectEntered.AddListener(OnGrab);
        }
    }

    private void OnDestroy()
    {
        if (interactors == null) return;
        foreach (var interactor in interactors)
        {
            if (interactor != null)
            {
                interactor.selectEntered.RemoveListener(OnGrab);
            }
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        Debug.Log($"<color=magenta>[GLOBAL DEBUG]</color> '{args.interactorObject.transform.name}' grabbed: '{args.interactableObject.transform.name}'");
    }
}
