using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

public class VRSceneFader : MonoBehaviour
{
    public static VRSceneFader Instance;

    [Header("Full Scene Loading")]
    [Tooltip("How long it takes to fade to pitch black when leaving a scene (seconds)")]
    public float fadeToBlackDuration = 1.0f;
    [Tooltip("How long it takes to wake up from pitch black when entering a scene (seconds)")]
    public float fadeToClearDuration = 1.5f;
    
    [Header("Fast Blink Preview")]
    [Tooltip("How fast it blinks to black when previewing a map")]
    public float blinkToBlackDuration = 0.15f;
    [Tooltip("How fast it wakes back up from the blink")]
    public float blinkToClearDuration = 0.25f;

    [Header("Colors")]
    [Tooltip("The color of the fade (pitch black is best for VR)")]
    public Color fadeColor = Color.black;

    private Image fadeImage;

    [Tooltip("Drag a custom Canvas or GameObject here that you want to appear when the game pauses (optional)")]
    public GameObject pauseCanvas;

    private void Awake()
    {
        // Simple Singleton so other scripts can talk to it easily
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        // Auto-generate the VR Canvas so you don't have to build any UI!
        GameObject canvasObj = new GameObject("VR_Fade_Canvas");
        canvasObj.transform.SetParent(this.transform);
        canvasObj.transform.localPosition = new Vector3(0, 0, 0.2f); // 20cm in front of eyes
        canvasObj.transform.localRotation = Quaternion.identity;
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 999; // Render over everything

        // Make it perfectly fit the vision
        RectTransform rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2f, 2f); // 2x2 meters, very large up close

        GameObject imageObj = new GameObject("Fade_Image");
        imageObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = imageObj.AddComponent<Image>();
        
        // Start pitch black so we wake up smoothly when the scene loads!
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f); 
        
        RectTransform imageRT = fadeImage.GetComponent<RectTransform>();
        imageRT.anchorMin = Vector2.zero;
        imageRT.anchorMax = Vector2.one;
        imageRT.offsetMin = Vector2.zero;
        imageRT.offsetMax = Vector2.zero;
        
        if (pauseCanvas != null)
        {
            pauseCanvas.SetActive(false);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Initial fade in when the game first launches
        StartCoroutine(FadeRoutine(1f, 0f, fadeToClearDuration));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Whenever a new scene finishes loading, fade back in!
        // This is crucial because this script now survives across scene transitions.
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1f, 0f, fadeToClearDuration));
    }

    /// <summary>
    /// Smoothly fades to black, fully waits for the fade to finish, then loads the scene.
    /// </summary>
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeAndLoadRoutine(sceneName));
    }

    private IEnumerator FadeAndLoadRoutine(string sceneName)
    {
        // 1. Fully complete the fade to black (wait for it!)
        yield return StartCoroutine(FadeRoutine(0f, 1f, fadeToBlackDuration));
        
        // 2. ONLY once it is perfectly pitch black, load the next scene!
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.LocalClientId == 0)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                // Send direct transport packet to Server
                FastBufferWriter writer = new FastBufferWriter(32, Unity.Collections.Allocator.Temp);
                using (writer)
                {
                    Unity.Collections.FixedString32Bytes safeName = new Unity.Collections.FixedString32Bytes(sceneName);
                    writer.WriteValueSafe(safeName);

                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                        "GlobalRequest_SceneChange",
                        NetworkManager.ServerClientId,
                        writer,
                        NetworkDelivery.Reliable
                    );
                }
            }
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// A fast 'blink' effect to hide jarring geometry swaps (like previewing maps).
    /// </summary>
    public void Blink(System.Action onBlinkMiddle)
    {
        StartCoroutine(BlinkRoutine(onBlinkMiddle));
    }

    private IEnumerator BlinkRoutine(System.Action onBlinkMiddle)
    {
        // Fast fade to black
        yield return StartCoroutine(FadeRoutine(0f, 1f, blinkToBlackDuration));
        
        // At pitch black, run whatever code we passed in (like swapping 3D map objects!)
        if (onBlinkMiddle != null) onBlinkMiddle.Invoke();

        // Fast fade back to clear
        yield return StartCoroutine(FadeRoutine(1f, 0f, blinkToClearDuration));
    }

    // The core math for fading smoothly over time
    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, timer / duration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, newAlpha);
            yield return null;
        }
        
        // Guarantee exact final alpha
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, endAlpha);
    }

    public void PausedVignette(bool isPaused)
    {
        if (fadeImage == null) return;
        if (pauseCanvas != null) pauseCanvas.SetActive(isPaused);

        if (isPaused)
        {
            StopAllCoroutines();
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0.8f);
        }
        else
        {
            StopAllCoroutines();
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        }
    }
}
