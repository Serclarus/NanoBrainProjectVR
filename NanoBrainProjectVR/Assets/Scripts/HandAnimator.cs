using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class HandAnimator : MonoBehaviour
{
    [Tooltip("The Grip input action from your XR Controller (e.g. XRI RightHand/Grip)")]
    public InputActionReference gripAction;
    
    [Tooltip("The Trigger input action from your XR Controller (e.g. XRI RightHand/Trigger)")]
    public InputActionReference triggerAction;

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (animator == null) return;

        // Read how hard the player is squeezing the grips and triggers
        float gripValue = gripAction.action.ReadValue<float>();
        float triggerValue = triggerAction.action.ReadValue<float>();

        // Send those values into the Unity Animator so it can blend the animations
        animator.SetFloat("Grip", gripValue);
        animator.SetFloat("Trigger", triggerValue);
    }
}
