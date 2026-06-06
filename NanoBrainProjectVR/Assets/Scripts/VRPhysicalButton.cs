using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class VRPhysicalButton : MonoBehaviour
{
    [Header("Button Events")]
    public UnityEvent onButtonPressed;
    
    [Tooltip("How many seconds before the button can be pressed again")]
    public float pressCooldown = 1.0f;
    private float lastPressTime = 0f;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("The visual 3D mesh of the button that will physically push inward")]
    public Transform buttonMesh;
    [Tooltip("How far inward the button pushes on the Z axis")]
    public float pressDistance = 0.02f;

    private Vector3 originalPos;

    private void Start()
    {
        if (buttonMesh != null)
        {
            originalPos = buttonMesh.localPosition;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Optional: If you only want the player's hands to press it, you can uncomment this!
        // if (!other.CompareTag("PlayerHand")) return;

        if (Time.time - lastPressTime > pressCooldown)
        {
            lastPressTime = Time.time;
            onButtonPressed?.Invoke();
            
            if (buttonMesh != null)
            {
                StopAllCoroutines();
                StartCoroutine(AnimatePress());
            }
        }
    }

    private IEnumerator AnimatePress()
    {
        // Push the button inward along its local Z axis
        buttonMesh.localPosition = originalPos + new Vector3(0, 0, pressDistance);
        
        yield return new WaitForSeconds(0.2f);
        
        // Pop it back out
        buttonMesh.localPosition = originalPos;
    }
}
