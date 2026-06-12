using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetMover : MonoBehaviour
{
    [Header("Random Movement Settings")]
    [Tooltip("Minimum movement speed (units per second).")]
    public float minMoveSpeed = 2f;
    [Tooltip("Maximum movement speed (units per second).")]
    public float maxMoveSpeed = 6f;
    [Tooltip("Minimum pause duration between moves.")]
    public float minPauseDuration = 0.5f;
    [Tooltip("Maximum pause duration between moves.")]
    public float maxPauseDuration = 2.5f;
    
    private Vector3 initialPosition;
    [HideInInspector]
    public bool isMovingRandomly = false;

    [HideInInspector]
    public bool isWaitingForSwap = false;

    // Multipliers for Phase 2
    [HideInInspector]
    public float currentSpeedMultiplier = 1f;
    [HideInInspector]
    public float currentPauseMultiplier = 1f;

    // Assigned by Manager
    private float minGlobalZ = 0f;
    private float maxGlobalZ = 30f;
    private int currentAssignedZone = -1;
    private bool forceNewDestination = false;

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
        
        initialPosition = transform.localPosition;
    }

    public void SetZone(int zoneIndex, float minZ, float maxZ)
    {
        currentAssignedZone = zoneIndex;
        minGlobalZ = minZ;
        maxGlobalZ = maxZ;
    }

    public int GetCurrentAssignedZone() => currentAssignedZone;

    public void ForceNewDestination()
    {
        forceNewDestination = true;
    }

    public void StartRandomMovement()
    {
        if (isMovingRandomly) return;
        isMovingRandomly = true;
        forceNewDestination = false;
        isWaitingForSwap = false;
        currentSpeedMultiplier = 1f;
        currentPauseMultiplier = 1f;
        StartCoroutine(RandomMoveRoutine());
    }

    public void StopAndReset()
    {
        isMovingRandomly = false;
        isWaitingForSwap = false;
        StopAllCoroutines();

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        transform.localPosition = initialPosition;
    }

    public float GetCurrentZDistance()
    {
        return transform.localPosition.z;
    }

    private IEnumerator RandomMoveRoutine()
    {
        while (isMovingRandomly)
        {
            forceNewDestination = false;

            // Pick a random target global Z position within bounds
            float targetZ = Random.Range(minGlobalZ, maxGlobalZ);
            Vector3 targetPosition = new Vector3(initialPosition.x, initialPosition.y, targetZ);

            // Pick a random speed and apply multiplier
            float speed = Random.Range(minMoveSpeed, maxMoveSpeed) * currentSpeedMultiplier;
            
            // Calculate how long it takes to get there at this speed
            float distance = Vector3.Distance(transform.localPosition, targetPosition);
            float duration = speed > 0 ? distance / speed : 0.1f;

            // Move there
            yield return StartCoroutine(MoveToPositionRoutine(targetPosition, duration));

            // Random pause and apply multiplier
            float pauseElapsed = 0f;
            float totalPause = Random.Range(minPauseDuration, maxPauseDuration) * currentPauseMultiplier;
            while (pauseElapsed < totalPause)
            {
                if (forceNewDestination) break;
                pauseElapsed += Time.deltaTime;
                yield return null;
            }

            // Always request to swap to a different zone!
            if (!forceNewDestination && ShootingRangeManager.Instance != null)
            {
                isWaitingForSwap = true;
                ShootingRangeManager.Instance.RequestZoneSwap(this, currentAssignedZone);

                // Wait until the manager approves the swap
                while (isWaitingForSwap && isMovingRandomly)
                {
                    yield return null;
                }
            }
        }
    }

    private IEnumerator MoveToPositionRoutine(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.localPosition;
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
            if (forceNewDestination) break; // Interrupted by manager

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smooth step for smoother start and stop
            t = t * t * (3f - 2f * t);

            transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        if (!forceNewDestination)
        {
            transform.localPosition = targetPosition;
        }

        // Stop moving sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
