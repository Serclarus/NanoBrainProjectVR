using UnityEngine;
using UnityEngine.SceneManagement;


public class DoorKnobSceneTransition : MonoBehaviour
{
    [Header("Transition Settings")]
    [Tooltip("Name of the scene to load when the knob is twisted (e.g., MainMenu)")]
    public string sceneToLoad = "MainMenu";

    [Tooltip("How many degrees the knob must be twisted to trigger the scene load")]
    public float twistThresholdAngle = 45f;

[Tooltip("If true, the scene will only load if the player is actively grabbing the knob (Only needed if using HingeJoint)")]
    public bool requireGrab = true;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip doorOpenSound;

    private HingeJoint hinge;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    private bool hasTriggered = false;

    private void Start()
    {
        hinge = GetComponent<HingeJoint>();
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(); 
    }

    private void Update()
    {
        if (hasTriggered) return;

        // If using a HingeJoint, check the angle physically
        if (hinge != null)
        {
            if (requireGrab && interactable != null && !interactable.isSelected)
            {
                return;
            }

            if (Mathf.Abs(hinge.angle) >= twistThresholdAngle)
            {
                TriggerTransition();
            }
        }
    }

    /// <summary>
    /// If you are using Unity's built-in XRKnob component, you can link the XRKnob's 
    /// "On Value Change" event to this method in the inspector!
    /// </summary>
    public void OnKnobValueChanged(float knobValue)
    {
        if (hasTriggered) return;

        // XRKnob usually outputs a value between 0 and 1.
        // Let's trigger if it's twisted at least 80% of the way.
        if (knobValue >= 0.8f)
        {
            TriggerTransition();
        }
    }

    public void TriggerTransition()
    {
        if (hasTriggered) return;
        
        hasTriggered = true;

        if (audioSource != null && doorOpenSound != null)
        {
            audioSource.PlayOneShot(doorOpenSound);
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
