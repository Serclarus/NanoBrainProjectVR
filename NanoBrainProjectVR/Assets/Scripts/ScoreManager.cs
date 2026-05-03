using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private int localPlayerScore = 0;
    public float currentMultiplier = 1.0f;

    [Header("UI References")]
    [Tooltip("Optional: Drag a TextMeshProUGUI element here to display score")]
    public TMP_Text scoreTextUI;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        UpdateScoreUI();
    }

    public void AddScore(int points)
    {
        int finalPoints = Mathf.RoundToInt(points * currentMultiplier);
        localPlayerScore += finalPoints;
        Debug.Log($"Score Added: +{finalPoints} (Base: {points}, Mult: {currentMultiplier}) | Total Score: {localPlayerScore}");
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreTextUI != null)
        {
            scoreTextUI.text = "Score: " + localPlayerScore.ToString();
        }
    }
}
