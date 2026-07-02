using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShootingRangeManager : MonoBehaviour
{
    public static ShootingRangeManager Instance { get; private set; }

    [Header("Global Settings")]
    public bool isShootingAllowed = true;
    [Tooltip("Duration of the shooting range game in seconds.")]
    public float gameDuration = 60f;

    [Header("Phase 2 Settings")]
    [Tooltip("Time remaining (in seconds) when Phase 2 starts.")]
    public float phase2TimeThreshold = 20f;
    [Tooltip("Multiplier applied to target movement speed during Phase 2.")]
    public float phase2SpeedMultiplier = 1.5f;
    [Tooltip("Multiplier applied to target pause duration during Phase 2.")]
    public float phase2PauseMultiplier = 0.5f;

    [Header("References")]
    [Tooltip("The target movers that will be controlled by this manager.")]
    public TargetMover[] targetMovers;
    
    [Tooltip("Optional: Text element to display the timer.")]
    public TMP_Text statusText;

    [Header("Start Buttons")]
    [Tooltip("The TextMeshPro on the physical start buttons to update (Start, 3, 2, 1, Running...)")]
    public TMP_Text[] startButtonTexts;
    private Color originalTextColor = Color.white;

    [Header("Lighting Feedback")]
    [Tooltip("Lights to change color based on game state.")]
    public Light[] rangeLights;
    public Color idleLightColor = Color.red;
    public Color activeLightColor = Color.green;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    [Tooltip("Sound played for each of the last 3 seconds of the game.")]
    public AudioClip countdownTickSound;
    [Tooltip("Sound played when the game starts/ends.")]
    public AudioClip phaseChangeSound;

    private bool isGameActive = false;
    private bool isCountingDown = false;
    private float currentTimer = 0f;
    private float countdownTimer = 0f;
    private int lastTickSecond = -1;
    private bool isPhase2Active = false;

    // Advanced Zone System
    [System.Serializable]
    public struct ScoreZone
    {
        [Tooltip("The maximum global Z distance for this zone.")]
        public float maxZDistance;
        [Tooltip("The score multiplier applied when a target in this zone is hit.")]
        public float multiplier;
    }

    [Header("Score Zones")]
    [Tooltip("The absolute minimum Z distance where the first zone starts.")]
    public float rangeStartZ = 0f;

    [Tooltip("Configure the zones. Targets will move within these bounds and grant these multipliers.")]
    public ScoreZone[] scoreZones = new ScoreZone[] {
        new ScoreZone { maxZDistance = 10f, multiplier = 1.2f },
        new ScoreZone { maxZDistance = 16f, multiplier = 1.3f },
        new ScoreZone { maxZDistance = 22f, multiplier = 1.6f },
        new ScoreZone { maxZDistance = 26f, multiplier = 1.8f },
        new ScoreZone { maxZDistance = 32f, multiplier = 2.5f }
    };

    private int MIN_TARGETS_PER_ZONE = 2;

    private class SwapRequest
    {
        public TargetMover target;
        public int fromZone;
        public int toZone;
        public bool volunteerDispatched;
    }

    private List<SwapRequest> pendingSwaps = new List<SwapRequest>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (startButtonTexts != null && startButtonTexts.Length > 0 && startButtonTexts[0] != null)
        {
            originalTextColor = startButtonTexts[0].color;
        }

        SetLightsColor(idleLightColor);
    }

    [ContextMenu("Start Shooting Range")]
    public void StartRange()
    {
        if (isGameActive || isCountingDown) return;

        // Reset the score whenever we press start
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetCurrentScore();
        }

        isCountingDown = true;
        countdownTimer = 3f; // 3 second countdown
        lastTickSecond = 4; // Start out of bounds to guarantee the first tick plays
    }

    private void BeginActualGame()
    {
        isGameActive = true;
        isShootingAllowed = true;
        currentTimer = gameDuration;
        lastTickSecond = Mathf.CeilToInt(currentTimer);
        isPhase2Active = false;
        pendingSwaps.Clear();

        SetLightsColor(activeLightColor);

        // Initialize target distributions
        List<TargetMover> unassigned = new List<TargetMover>();
        foreach(var t in targetMovers) 
        {
            if (t != null) unassigned.Add(t);
        }

        int numZones = scoreZones.Length;
        for (int i = 0; i < numZones; i++)
        {
            for (int j = 0; j < MIN_TARGETS_PER_ZONE; j++)
            {
                if (unassigned.Count > 0)
                {
                    int randIdx = Random.Range(0, unassigned.Count);
                    TargetMover t = unassigned[randIdx];
                    unassigned.RemoveAt(randIdx);
                    t.SetZone(i, GetZoneMinZ(i), GetZoneMaxZ(i));
                    t.StartRandomMovement();
                }
            }
        }

        // Remaining targets roam
        foreach (var t in unassigned)
        {
            int randZone = Random.Range(0, numZones);
            t.SetZone(randZone, GetZoneMinZ(randZone), GetZoneMaxZ(randZone));
            t.StartRandomMovement();
        }

        PlayStateChangeSound();
        UpdateUI();
    }

    private void Update()
    {
        if (isCountingDown)
        {
            countdownTimer -= Time.deltaTime;
            int currentCount = Mathf.CeilToInt(countdownTimer);

            // Play a tick sound every time the number changes
            if (currentCount != lastTickSecond && currentCount > 0)
            {
                lastTickSecond = currentCount;
                PlayCountdownTick();
            }

            if (startButtonTexts != null)
            {
                foreach (var text in startButtonTexts)
                {
                    if (text != null) text.text = currentCount.ToString();
                }
            }

            if (countdownTimer <= 0f)
            {
                isCountingDown = false;
                if (startButtonTexts != null)
                {
                    foreach (var text in startButtonTexts)
                    {
                        if (text != null) text.text = "Running...";
                    }
                }
                BeginActualGame();
            }
            return;
        }

        if (!isGameActive) return;

        currentTimer -= Time.deltaTime;

        // Check for Phase 2 activation
        if (!isPhase2Active && currentTimer <= phase2TimeThreshold)
        {
            isPhase2Active = true;
            foreach (var t in targetMovers)
            {
                if (t != null)
                {
                    t.currentSpeedMultiplier = phase2SpeedMultiplier;
                    t.currentPauseMultiplier = phase2PauseMultiplier;
                }
            }
        }

        ManageAdvancedZones();

        // Play countdown tick for the last 3 seconds
        int currentSecond = Mathf.CeilToInt(currentTimer);
        if (currentSecond <= 3 && currentSecond > 0 && currentSecond != lastTickSecond)
        {
            lastTickSecond = currentSecond;
            PlayCountdownTick();
        }

        if (currentTimer <= 0f)
        {
            currentTimer = 0f;
            EndGame();
        }
        else
        {
            UpdateUI();
        }
    }

    private void ManageAdvancedZones()
    {
        int numZones = scoreZones.Length;
        // 1. Calculate physical count in each zone
        int[] physicalCounts = new int[numZones];
        foreach (var t in targetMovers)
        {
            if (t == null) continue;
            float z = t.GetCurrentZDistance();
            int zIndex = GetZoneIndex(z);
            if (zIndex >= 0 && zIndex < numZones)
            {
                physicalCounts[zIndex]++;
            }
        }

        // 2. Process pending swaps
        for (int i = pendingSwaps.Count - 1; i >= 0; i--)
        {
            var swap = pendingSwaps[i];
            
            // If the fromZone has plenty of targets right now, approve the swap!
            if (physicalCounts[swap.fromZone] > MIN_TARGETS_PER_ZONE)
            {
                swap.target.SetZone(swap.toZone, GetZoneMinZ(swap.toZone), GetZoneMaxZ(swap.toZone));
                if (swap.target.isWaitingForSwap)
                {
                    swap.target.isWaitingForSwap = false;
                }
                else
                {
                    swap.target.ForceNewDestination();
                }
                pendingSwaps.RemoveAt(i);
                // Adjust physical count instantly to prevent double-spending the surplus
                physicalCounts[swap.fromZone]--;
                continue;
            }

            // If we haven't dispatched a volunteer yet, find one
            if (!swap.volunteerDispatched)
            {
                int surplusZone = -1;
                for (int z = 0; z < numZones; z++)
                {
                    if (physicalCounts[z] > MIN_TARGETS_PER_ZONE && z != swap.fromZone)
                    {
                        surplusZone = z;
                        break;
                    }
                }

                if (surplusZone != -1)
                {
                    TargetMover volunteer = null;
                    foreach (var t in targetMovers)
                    {
                        if (t != null && t != swap.target && GetZoneIndex(t.GetCurrentZDistance()) == surplusZone)
                        {
                            volunteer = t;
                            break;
                        }
                    }

                    if (volunteer != null)
                    {
                        // Dispatch volunteer to the deficient zone
                        if (volunteer.isWaitingForSwap)
                        {
                            // Hijack its pending swap request if it has one
                            for (int p = pendingSwaps.Count - 1; p >= 0; p--)
                            {
                                if (pendingSwaps[p].target == volunteer)
                                {
                                    pendingSwaps.RemoveAt(p);
                                }
                            }
                            volunteer.SetZone(swap.fromZone, GetZoneMinZ(swap.fromZone), GetZoneMaxZ(swap.fromZone));
                            volunteer.isWaitingForSwap = false;
                        }
                        else
                        {
                            // Interrupt moving target
                            volunteer.SetZone(swap.fromZone, GetZoneMinZ(swap.fromZone), GetZoneMaxZ(swap.fromZone));
                            volunteer.ForceNewDestination();
                        }

                        swap.volunteerDispatched = true;
                        physicalCounts[surplusZone]--;
                    }
                }
            }
        }
    }

    public void RequestZoneSwap(TargetMover target, int currentZone)
    {
        if (!isGameActive) return;

        int numZones = scoreZones.Length;
        if (numZones <= 1) return;

        // Guarantee a different zone
        int newZone;
        do
        {
            newZone = Random.Range(0, numZones);
        } while (newZone == currentZone);

        foreach (var p in pendingSwaps)
        {
            if (p.target == target) return;
        }

        pendingSwaps.Add(new SwapRequest {
            target = target,
            fromZone = currentZone,
            toZone = newZone,
            volunteerDispatched = false
        });
    }

    private int GetZoneIndex(float z)
    {
        int numZones = scoreZones.Length;
        for (int i = 0; i < numZones; i++)
        {
            if (z <= scoreZones[i].maxZDistance) return i;
        }
        return numZones - 1;
    }

    public float GetMultiplierForZ(float z)
    {
        int zIndex = GetZoneIndex(z);
        if (zIndex >= 0 && zIndex < scoreZones.Length)
        {
            return scoreZones[zIndex].multiplier;
        }
        return 1.0f;
    }

    private float GetZoneMinZ(int index)
    {
        if (index <= 0) return rangeStartZ;
        return scoreZones[index - 1].maxZDistance;
    }
    
    private float GetZoneMaxZ(int index)
    {
        if (index < 0 || index >= scoreZones.Length) return scoreZones[scoreZones.Length - 1].maxZDistance;
        return scoreZones[index].maxZDistance;
    }

    private void EndGame()
    {
        isGameActive = false;
        isShootingAllowed = false;
        
        if (startButtonTexts != null)
        {
            foreach (var text in startButtonTexts)
            {
                if (text != null)
                {
                    text.text = "Start";
                    text.color = originalTextColor;
                }
            }
        }

        SetLightsColor(idleLightColor);
        PlayStateChangeSound();
        UpdateUI();

        foreach (var mover in targetMovers)
        {
            if (mover != null) mover.StopAndReset();
        }
    }

    private void PlayCountdownTick()
    {
        if (audioSource != null && countdownTickSound != null)
        {
            audioSource.PlayOneShot(countdownTickSound);
        }
    }

    private void PlayStateChangeSound()
    {
        if (audioSource != null && phaseChangeSound != null)
        {
            audioSource.PlayOneShot(phaseChangeSound);
        }
    }

    private void SetLightsColor(Color newColor)
    {
        if (rangeLights == null) return;
        foreach (var light in rangeLights)
        {
            if (light != null)
            {
                light.color = newColor;
            }
        }
    }

    private void UpdateUI()
    {
        if (statusText != null)
        {
            float time = Mathf.Max(0, currentTimer);
            int minutes = Mathf.FloorToInt(time / 60F);
            int seconds = Mathf.FloorToInt(time - minutes * 60);
            statusText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
