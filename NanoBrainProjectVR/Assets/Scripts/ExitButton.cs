using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using XRMultiplayer;

public class ExitButton : MonoBehaviour
{
    [Header("Exit Settings")]
    [Tooltip("The TextMeshPro text on the button")]
    public TMP_Text buttonText;
    
    [Tooltip("The exact name of the Main Menu scene to load")]
    public string mainMenuSceneName = "MainMenu";
    
    [Header("Colors (HDR Supported)")]
    [Tooltip("The default color of the text.")]
    [ColorUsage(true, true)]
    public Color defaultColor = Color.white;
    
    [Tooltip("The color when asking 'Sure?'. Turn up intensity for Bloom!")]
    [ColorUsage(true, true)]
    public Color confirmColor = new Color(0f, 0f, 2f, 1f); // HDR Blue

    [Tooltip("The color when 'Loading...'.")]
    [ColorUsage(true, true)]
    public Color loadingColor = new Color(2f, 2f, 0f, 1f); // HDR Yellow

    private bool isConfirming = false;
    private bool isLoading = false;
    private Coroutine cancelCoroutine;

    private Color originalFaceColor;

    // A tiny cooldown so they don't double-touch it instantly
    private float lastTouchTime = 0f;
    private float touchCooldown = 0.5f; // Half a second before they can confirm

    private void Start()
    {
        if (buttonText != null)
        {
            originalFaceColor = buttonText.fontMaterial.GetColor(ShaderUtilities.ID_FaceColor);
        }
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
                buttonText.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, confirmColor);
                buttonText.UpdateMeshPadding();
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
                buttonText.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, loadingColor);
                buttonText.UpdateMeshPadding();
            }

            Debug.Log($"Exit Button confirmed! Disconnecting and returning to {mainMenuSceneName}...");

            // Properly disconnect from the multiplayer session if one exists!
            // (Using reflection-safe check in case the script is missing)
            if (XRINetworkGameManager.Instance != null)
            {
                XRINetworkGameManager.Instance.LeaveLocalConnection();
            }

            // Use the smooth VR fader if it exists!
            if (VRSceneFader.Instance != null)
            {
                VRSceneFader.Instance.FadeToScene(mainMenuSceneName);
            }
            else
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
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
            buttonText.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, defaultColor);
            buttonText.UpdateMeshPadding();
        }
    }
}
