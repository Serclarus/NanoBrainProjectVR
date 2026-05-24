using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public class ExitDoorHandle : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("The XR Knob component attached to your door handle")]
    public XRKnob doorKnob;
    
    [Tooltip("How far the knob needs to be turned (0.0 to 1.0) to open the door. 0.8 = 80% turned.")]
    [Range(0f, 1f)]
    public float turnThreshold = 0.8f;

    [Tooltip("The exact name of your Main Menu scene")]
    public string mainMenuSceneName = "MainMenu";

    private bool hasOpened = false;

    private void Start()
    {
        if (doorKnob == null)
        {
            doorKnob = GetComponent<XRKnob>();
        }

        if (doorKnob != null)
        {
            // Listen to the knob turning in real-time
            doorKnob.onValueChange.AddListener(OnKnobTurned);
        }
        else
        {
            Debug.LogError("ExitDoorHandle needs an XR Knob component to work!");
        }
    }

    private void OnDestroy()
    {
        if (doorKnob != null)
        {
            doorKnob.onValueChange.RemoveListener(OnKnobTurned);
        }
    }

    private void OnKnobTurned(float turnAmount)
    {
        // Prevent loading the scene 100 times in a row
        if (hasOpened) return;

        // If they turned the knob past the threshold, open the door!
        if (turnAmount >= turnThreshold)
        {
            hasOpened = true;
            Debug.Log($"Door opened! Returning to {mainMenuSceneName}...");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
