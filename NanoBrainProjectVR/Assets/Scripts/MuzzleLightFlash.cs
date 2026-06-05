using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class MuzzleLightFlash : MonoBehaviour
{
    private Light muzzleLight;
    
    [Tooltip("How long the light stays on in seconds")]
    public float flashDuration = 0.05f;
    
    [Tooltip("The maximum brightness of the flash")]
    public float maxIntensity = 2.0f;

    private Coroutine flashCoroutine;

    private void Awake()
    {
        muzzleLight = GetComponent<Light>();
        muzzleLight.enabled = false;
    }

    private void OnEnable()
    {
        if (muzzleLight != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        muzzleLight.intensity = maxIntensity;
        muzzleLight.enabled = true;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            // Quickly fade the light out for realism
            muzzleLight.intensity = Mathf.Lerp(maxIntensity, 0f, elapsed / flashDuration);
            yield return null;
        }

        muzzleLight.enabled = false;
    }
}
