using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.XR.Interaction.Toolkit;
#endif

public class BoarHuntingManager : MonoBehaviour
{
    [Header("Spawning Settings")]
    public GameObject boarPrefab;
    public Transform[] spawnPoints;
    public int maxBoarsPerSession = 5;

    [Header("UI Settings")]
    public Canvas endGameCanvas;
    [Tooltip("How far in front of the player the End Game UI should appear")]
    public float endGameUIDistance = 0.4f;
    public TMP_Text huntedText;
    public TMP_Text timeText;
    public TMP_Text accuracyText;

    private int totalSpawned = 0;
    private int huntedBoars = 0;
    private int fledBoars = 0;
    private int totalShotsFired = 0;
    private int totalHits = 0;
    private float gameStartTime;
    private bool gameEnded = false;

    private List<GameObject> activeBoars = new List<GameObject>();
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        if (endGameCanvas != null) endGameCanvas.gameObject.SetActive(false);
        gameStartTime = Time.time;

        WeaponController.OnProjectilesFired += HandleShotFired;

        // Register the boars that are already placed in the scene
        BoarAI[] existingBoars = FindObjectsOfType<BoarAI>();
        foreach (BoarAI boar in existingBoars)
        {
            RegisterBoar(boar.gameObject);
        }

        StartCoroutine(SpawnRoutine());
    }

    private void OnDestroy()
    {
        WeaponController.OnProjectilesFired -= HandleShotFired;
    }

    private void HandleShotFired(int amount)
    {
        if (!gameEnded)
        {
            totalShotsFired += amount;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        maxBoarsPerSession = 5;
        
        // 1. Wait 15 seconds, then spawn the 3rd boar unconditionally
        yield return new WaitForSeconds(15f);
        
        // Only spawn the 15-second boar if we haven't already hit the limit
        if (huntedBoars + fledBoars + activeBoars.Count < maxBoarsPerSession)
        {
            SpawnBoar();
        }

        // 2. Keep checking if we need to spawn more
        while (true)
        {
            // Clean up list just in case
            activeBoars.RemoveAll(b => b == null);
            
            if (huntedBoars + fledBoars >= maxBoarsPerSession)
            {
                break; // Game is over!
            }

            // If there's 1 or 0 boars on the map, spawn a new one to replace it
            if (activeBoars.Count <= 1)
            {
                yield return new WaitForSeconds(7f);
                
                // Bulletproof check: If the total number of boars (dead + fled + currently alive) is less than 5, spawn a new one!
                if (huntedBoars + fledBoars + activeBoars.Count < maxBoarsPerSession)
                {
                    SpawnBoar();
                }
            }
            yield return new WaitForSeconds(1f); // Check every second
        }
    }

    private void SpawnBoar()
    {
        if (boarPrefab == null || spawnPoints.Length == 0) return;

        Transform chosenPoint = GetBestSpawnPoint();
        
        // Spawn as a child of this manager so SendMessageUpwards works!
        GameObject newBoar = Instantiate(boarPrefab, chosenPoint.position, chosenPoint.rotation, this.transform);
        RegisterBoar(newBoar);
    }

    private void RegisterBoar(GameObject boarObj)
    {
        if (!activeBoars.Contains(boarObj))
        {
            activeBoars.Add(boarObj);
            totalSpawned++;
        }

        // Hook into AnimalHealth. Try to use the explicit reference from BoarAI first!
        BoarAI ai = boarObj.GetComponent<BoarAI>();
        AnimalHealth health = (ai != null && ai.healthScript != null) ? ai.healthScript : boarObj.GetComponentInChildren<AnimalHealth>();
        
        if (health != null)
        {
            health.onDamageTaken.AddListener(() => {
                if (!health.isDead && !gameEnded) totalHits++;
            });
            
            health.onDeathEvent.AddListener(() => {
                if (gameEnded) return;
                huntedBoars++;
                activeBoars.Remove(boarObj);
                CheckEndCondition();
            });
        }
    }

    public void OnBoarFled(GameObject fledBoar)
    {
        // Called via SendMessageUpwards from BoarAI
        if (gameEnded) return;
        fledBoars++;
        // Instantly remove the boar from the active list so the spawner knows it is gone immediately!
        if (fledBoar != null)
        {
            activeBoars.Remove(fledBoar);
        }
        CheckEndCondition();
    }

    private void CheckEndCondition()
    {
        if (gameEnded) return;

        if (huntedBoars + fledBoars >= maxBoarsPerSession)
        {
            EndGame();
        }
    }

    private Transform GetBestSpawnPoint()
    {
        if (mainCamera == null) return spawnPoints[Random.Range(0, spawnPoints.Length)];

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
        List<Transform> hiddenPoints = new List<Transform>();
        Transform furthestPoint = spawnPoints[0];
        float maxDist = -1f;

        foreach (Transform point in spawnPoints)
        {
            // Check if point is inside camera frustum (visible)
            bool isVisible = GeometryUtility.TestPlanesAABB(planes, new Bounds(point.position, Vector3.one * 2f));
            
            if (!isVisible)
            {
                hiddenPoints.Add(point);
            }

            float dist = Vector3.Distance(point.position, mainCamera.transform.position);
            if (dist > maxDist)
            {
                maxDist = dist;
                furthestPoint = point;
            }
        }

        // If there are points the player cannot see, pick a random one of those
        if (hiddenPoints.Count > 0)
        {
            return hiddenPoints[Random.Range(0, hiddenPoints.Count)];
        }

        // If player sees all points, pick the furthest one
        return furthestPoint;
    }

    private void EndGame()
    {
        gameEnded = true;

        // 1. Disable Player Movement
#if ENABLE_INPUT_SYSTEM
        UnityEngine.XR.Interaction.Toolkit.ActionBasedContinuousMoveProvider moveProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.ActionBasedContinuousMoveProvider>();
        if (moveProvider != null)
        {
            moveProvider.enabled = false;
        }
#endif

        // 2. Show UI
        if (endGameCanvas != null)
        {
            // Position canvas in front of player
            if (mainCamera != null)
            {
                Vector3 flatForward = mainCamera.transform.forward;
                flatForward.y = 0;
                flatForward.Normalize();
                
                endGameCanvas.transform.position = mainCamera.transform.position + flatForward * endGameUIDistance;
                endGameCanvas.transform.position = new Vector3(endGameCanvas.transform.position.x, mainCamera.transform.position.y, endGameCanvas.transform.position.z);
                
                endGameCanvas.transform.rotation = Quaternion.LookRotation(endGameCanvas.transform.position - mainCamera.transform.position);
            }
            
            endGameCanvas.gameObject.SetActive(true);
        }

        // 3. Populate Texts
        if (huntedText != null)
        {
            huntedText.text = $"{huntedBoars}/{maxBoarsPerSession}";
        }

        if (timeText != null)
        {
            float duration = Time.time - gameStartTime;
            int minutes = Mathf.FloorToInt(duration / 60F);
            int seconds = Mathf.FloorToInt(duration - minutes * 60);
            timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        if (accuracyText != null)
        {
            float accuracy = 0f;
            if (totalShotsFired > 0)
            {
                // Ensure accuracy doesn't exceed 100% (e.g. shotgun spread multi-hits)
                accuracy = Mathf.Min(100f, ((float)totalHits / totalShotsFired) * 100f);
            }
            accuracyText.text = string.Format("%{0:0}", accuracy);
        }

        StartCoroutine(FadeInAndEnableButtons(endGameCanvas.gameObject, 1f));
    }

    private IEnumerator FadeInAndEnableButtons(GameObject canvasObj, float duration)
    {
        CanvasGroup cg = canvasObj.GetComponent<CanvasGroup>();
        if (cg == null) cg = canvasObj.AddComponent<CanvasGroup>();
        
        UnityEngine.UI.Button[] buttons = canvasObj.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons) btn.interactable = false;
        
        // Disable physical VR colliders so hands don't instantly poke them!
        Collider[] colliders = canvasObj.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders) col.enabled = false;

        cg.alpha = 0f;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, time / duration);
            yield return null;
        }
        cg.alpha = 1f;

        // Wait an extra 1.5 seconds before letting them click anything
        yield return new WaitForSeconds(1.5f);

        foreach (var btn in buttons) btn.interactable = true;
        foreach (var col in colliders) col.enabled = true;
    }

    // --- UI Button Methods ---
    
    public void RestartLevel()
    {
#if ENABLE_INPUT_SYSTEM
        UnityEngine.XR.Interaction.Toolkit.ActionBasedContinuousMoveProvider moveProvider = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.ActionBasedContinuousMoveProvider>();
        if (moveProvider != null)
        {
            moveProvider.enabled = true;
        }
#endif

        if (VRSceneFader.Instance != null)
        {
            VRSceneFader.Instance.FadeToScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void ReturnToHub(string hubSceneName)
    {
        if (VRSceneFader.Instance != null)
        {
            VRSceneFader.Instance.FadeToScene(hubSceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(hubSceneName);
        }
    }
}
