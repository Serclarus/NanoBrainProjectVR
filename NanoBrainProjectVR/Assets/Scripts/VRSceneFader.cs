using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VRSceneFader : MonoBehaviour
{
    public static VRSceneFader Instance;

    [Header("Fade Settings")]
    [Tooltip("How long it takes to fade to complete black when changing scenes (seconds)")]
    public float sceneFadeDuration = 1.0f;
    
    [Tooltip("How long it takes for a quick 'blink' effect when previewing maps (seconds)")]
    public float blinkDuration = 0.15f;

    [Tooltip("The color of the fade (pitch black is best for VR)")]
    public Color fadeColor = Color.black;

    private Image fadeImage;

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
    }

    private void Start()
    {
        // Whenever a scene finishes loading, instantly fade from black to clear!
        StartCoroutine(FadeRoutine(1f, 0f, sceneFadeDuration));
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
        yield return StartCoroutine(FadeRoutine(0f, 1f, sceneFadeDuration));
        
        // 2. ONLY once it is perfectly pitch black, load the next scene!
        SceneManager.LoadScene(sceneName);
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
        yield return StartCoroutine(FadeRoutine(0f, 1f, blinkDuration));
        
        // At pitch black, run whatever code we passed in (like swapping 3D map objects!)
        if (onBlinkMiddle != null) onBlinkMiddle.Invoke();

        // Fast fade back to clear
        yield return StartCoroutine(FadeRoutine(1f, 0f, blinkDuration));
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
}
