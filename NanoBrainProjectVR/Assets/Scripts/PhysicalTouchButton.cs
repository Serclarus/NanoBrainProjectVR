using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using TMPro;
using XRMultiplayer;

public class PhysicalTouchButton : MonoBehaviour
{
    [Tooltip("The Unity Event to fire when the button is physically touched by a player's hand.")]
    public UnityEvent onButtonTouch;

    [Tooltip("To prevent accidental double-touches, wait this many seconds before the button can be pressed again.")]
    public float cooldownDelay = 1.0f;
    
    [Header("Haptics")]
    [Tooltip("Intensity of the vibration when touched (0.0 to 1.0)")]
    public float hapticIntensity = 0.5f;
    [Tooltip("Duration of the vibration in seconds")]
    public float hapticDuration = 0.1f;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("Assign a TextMeshPro component (3D or UI) to change its color during connection")]
    public TMP_Text buttonText;
    private Color originalColor;
    private bool wasTouched = false;

    private float lastPressTime = 0f;

    private void Start()
    {
        if (buttonText != null)
        {
            originalColor = buttonText.color;
            
            // Subscribe to the network manager's state changes so we know when it connects
            XRINetworkGameManager.CurrentConnectionState.Subscribe(OnConnectionStateChanged);
        }
    }

    private void OnDestroy()
    {
        if (buttonText != null)
        {
            XRINetworkGameManager.CurrentConnectionState.Unsubscribe(OnConnectionStateChanged);
        }
    }

    private void OnConnectionStateChanged(XRINetworkGameManager.ConnectionState state)
    {
        Debug.Log($"[ColorDebug] OnConnectionStateChanged triggered! New State: {state}, wasTouched: {wasTouched}, buttonText assigned: {buttonText != null}");

        if (buttonText == null || !wasTouched) return;

        // If this specific button was touched, update its visuals based on the network state
        if (state == XRINetworkGameManager.ConnectionState.Connecting || 
            state == XRINetworkGameManager.ConnectionState.Authenticating)
        {
            Debug.Log("[ColorDebug] Changing color to Blue!");
            buttonText.color = Color.blue;
        }
        else if (state == XRINetworkGameManager.ConnectionState.Connected)
        {
            Debug.Log("[ColorDebug] Changing color to Green!");
            buttonText.color = Color.green;
        }
        else if (state == XRINetworkGameManager.ConnectionState.None)
        {
            Debug.Log("[ColorDebug] Resetting color!");
            // If disconnected, reset the button
            buttonText.color = originalColor;
            wasTouched = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore if the button is on cooldown
        if (Time.time < lastPressTime + cooldownDelay) return;

        // Check if the object touching us is part of the player or hands
        if (other.CompareTag("Player") || other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller"))
        {
            Debug.Log($"[ColorDebug] Button Touched by {other.name}! Current State: {XRINetworkGameManager.CurrentConnectionState.Value}");
            lastPressTime = Time.time;
            wasTouched = true; // Mark this specific button as the one that was pressed
            
            // If the state is currently 'None', pre-emptively turn it blue so it reacts instantly to the touch
            if (buttonText != null && XRINetworkGameManager.CurrentConnectionState.Value == XRINetworkGameManager.ConnectionState.None)
            {
                Debug.Log("[ColorDebug] Preemptively turning Blue!");
                buttonText.color = Color.blue;
            }

            onButtonTouch.Invoke();

            // Try to find the VR controller interactor on the hand to trigger vibration
            XRBaseInputInteractor interactor = other.GetComponentInParent<XRBaseInputInteractor>();
            if (interactor != null)
            {
                interactor.SendHapticImpulse(hapticIntensity, hapticDuration);
            }
        }
    }
}
