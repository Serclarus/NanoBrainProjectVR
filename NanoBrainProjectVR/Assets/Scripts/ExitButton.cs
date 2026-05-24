using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class ExitButton : MonoBehaviour
{
    [Header("Exit Settings")]
    [Tooltip("The TextMeshPro text on the button")]
    public TMP_Text buttonText;
    
    [Tooltip("The exact name of the Main Menu scene to load")]
    public string mainMenuSceneName = "MainMenu";
    
    [Header("Colors")]
    public Color defaultColor = Color.white;
    public Color confirmColor = Color.blue;

    private bool isConfirming = false;
    private bool isLoading = false;
    private Coroutine cancelCoroutine;

    // A tiny cooldown so they don't double-touch it instantly
    private float lastTouchTime = 0f;
    private float touchCooldown = 0.5f; // Half a second before they can confirm

    private void Start()
    {
        ResetButton();
    }

    // Automatically detects your physical ghost hands!
    private void OnTriggerEnter(Collider other)
    {
        // Ignore if we are already loading, or if they just touched it a millisecond ago
        if (isLoading || Time.time < lastTouchTime + touchCooldown) return;

        // Only react if a hand/controller touches it
        if (other.CompareTag("Player") || other.name.ToLower().Contains("hand") || other.name.ToLower().Contains("controller"))
        {
            lastTouchTime = Time.time;
            HandleTouch();
        }
    }

    private void HandleTouch()
    {
        if (!isConfirming)
        {
            // --- FIRST TOUCH: CONFIRMATION MODE ---
            isConfirming = true;
            if (buttonText != null)
            {
                buttonText.text = "Sure?";
                buttonText.color = confirmColor;
            }

            // Start the 3-second timer
            if (cancelCoroutine != null) StopCoroutine(cancelCoroutine);
            cancelCoroutine = StartCoroutine(CancelConfirmationTimer());
        }
        else
        {
            // --- SECOND TOUCH: EXECUTE EXIT ---
            isLoading = true;
            if (cancelCoroutine != null) StopCoroutine(cancelCoroutine);

            if (buttonText != null)
            {
                buttonText.text = "Loading...";
                buttonText.color = Color.yellow;
            }

            Debug.Log($"Exit Button confirmed! Disconnecting and returning to {mainMenuSceneName}...");

            // Properly disconnect from the multiplayer session if one exists!
            // (Using reflection-safe check in case the script is missing)
            if (XRINetworkGameManager.Instance != null)
            {
                XRINetworkGameManager.Instance.LeaveLocalConnection();
            }

            // Load the Main Menu
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    // This runs in the background and counts to 3!
    private IEnumerator CancelConfirmationTimer()
    {
        yield return new WaitForSeconds(3.0f);
        
        // 3 seconds passed! Revert back to the normal "Exit" state.
        ResetButton();
    }

    private void ResetButton()
    {
        isConfirming = false;
        if (buttonText != null)
        {
            buttonText.text = "Exit";
            buttonText.color = defaultColor;
        }
    }
}
