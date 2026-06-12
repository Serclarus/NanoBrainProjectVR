using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetMover : MonoBehaviour
{
    [Header("Random Movement Settings")]
    [Tooltip("Minimum global Z position.")]
    public float minGlobalZ = 0f;
    [Tooltip("Maximum global Z position.")]
    public float maxGlobalZ = 30f;
    [Tooltip("Minimum movement speed (units per second).")]
    public float minMoveSpeed = 2f;
    [Tooltip("Maximum movement speed (units per second).")]
    public float maxMoveSpeed = 6f;
    [Tooltip("Minimum pause duration between moves.")]
    public float minPauseDuration = 0.5f;
    [Tooltip("Maximum pause duration between moves.")]
    public float maxPauseDuration = 2.5f;
    
    private Vector3 initialPosition;
    private bool isMovingRandomly = false;

    [Header("Audio Settings")]
    [Tooltip("AudioSource to play the movement sound.")]
    public AudioSource audioSource;
    [Tooltip("Sound to play while the target is moving.")]
    public AudioClip movingSound;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        initialPosition = transform.position;
    }

    public void StartRandomMovement()
    {
        if (isMovingRandomly) return;
        isMovingRandomly = true;
        StartCoroutine(RandomMoveRoutine());
    }

    public void StopAndReset()
    {
        isMovingRandomly = false;
        StopAllCoroutines();

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        transform.position = initialPosition;
    }

    public float GetCurrentZDistance()
    {
        return transform.position.z;
    }

    private IEnumerator RandomMoveRoutine()
    {
        while (isMovingRandomly)
        {
            // Pick a random target global Z position
            float targetZ = Random.Range(minGlobalZ, maxGlobalZ);
            Vector3 targetPosition = new Vector3(initialPosition.x, initialPosition.y, targetZ);

            // Pick a random speed
            float speed = Random.Range(minMoveSpeed, maxMoveSpeed);
            
            // Calculate how long it takes to get there at this speed
            float distance = Vector3.Distance(transform.position, targetPosition);
            float duration = distance / speed;

            // Move there
            yield return StartCoroutine(MoveToPositionRoutine(targetPosition, duration));

            // Random pause
            float pause = Random.Range(minPauseDuration, maxPauseDuration);
            yield return new WaitForSeconds(pause);
        }
    }

    private IEnumerator MoveToPositionRoutine(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        // Play moving sound
        if (audioSource != null && movingSound != null)
        {
            audioSource.clip = movingSound;
            audioSource.loop = true;
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smooth step for smoother start and stop
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;

        // Stop moving sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
