using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public float currentMultiplier = 1.0f;

    [Header("Networked Scores")]
    public NetworkVariable<int> hostScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> clientScore = new NetworkVariable<int>(0);
    
    // Fallback offline score
    private int offlineScore = 0;
    private int offlineHighScore = 0;

    [Header("UI References")]
    [Tooltip("Text element for Player 1 (Host)")]
    public TMP_Text player1ScoreText;
    
    [Tooltip("Text element for Player 2 (Client)")]
    public TMP_Text player2ScoreText;

    [Tooltip("Text element for Offline High Score")]
    public TMP_Text highScoreText;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        offlineHighScore = PlayerPrefs.GetInt("OfflineHighScore", 0);
        UpdateScoreUI();
    }

    public override void OnNetworkSpawn()
    {
        // When connecting to the server, the host resets the scores to 0
        if (IsServer)
        {
            hostScore.Value = 0;
            clientScore.Value = 0;
        }

        // Listen for score changes so the UI updates automatically
        hostScore.OnValueChanged += (oldVal, newVal) => UpdateScoreUI();
        clientScore.OnValueChanged += (oldVal, newVal) => UpdateScoreUI();
        
        UpdateScoreUI();
    }

    public void AddScore(int points, float customMultiplier = -1f)
    {
        float multiplierToUse = customMultiplier >= 0f ? customMultiplier : currentMultiplier;
        int finalPoints = Mathf.RoundToInt(points * multiplierToUse);

        if (!IsSpawned)
        {
            // If playing offline, just add to the offline score
            offlineScore += finalPoints;
            UpdateScoreUI();
            return;
        }

        // If online, send the points to the server with our unique Client ID
        AddScoreServerRpc(finalPoints, NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AddScoreServerRpc(int points, ulong clientId)
    {
        if (clientId == 0) // Client 0 is always the Host (Player 1)
        {
            hostScore.Value += points;
        }
        else // Client 1 is the joined Player 2
        {
            clientScore.Value += points;
        }
    }

    [ContextMenu("Reset Current Score")]
    public void ResetCurrentScore()
    {
        // Reset local offline score
        offlineScore = 0;

        if (IsSpawned)
        {
            // If online, tell the server to reset everyone's score
            ResetScoreServerRpc();
        }

        UpdateScoreUI();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ResetScoreServerRpc()
    {
        hostScore.Value = 0;
        clientScore.Value = 0;
    }

    private void UpdateScoreUI()
    {
        int myCurrentScore = 0;

        if (player1ScoreText != null)
        {
            if (IsSpawned)
            {
                player1ScoreText.text = hostScore.Value.ToString();
                if (IsServer) myCurrentScore = hostScore.Value; // Host's score
            }
            else
            {
                player1ScoreText.text = offlineScore.ToString();
                myCurrentScore = offlineScore; // Offline player's score
            }
        }

        if (player2ScoreText != null)
        {
            if (IsSpawned)
            {
                player2ScoreText.text = clientScore.Value.ToString();
                if (!IsServer) myCurrentScore = clientScore.Value; // Client's score
            }
            else
            {
                player2ScoreText.text = "0"; // Offline, P2 doesn't exist
            }
        }

        // Check if the current local player beat their high score!
        if (myCurrentScore > offlineHighScore)
        {
            offlineHighScore = myCurrentScore;
            PlayerPrefs.SetInt("OfflineHighScore", offlineHighScore);
            PlayerPrefs.Save();
        }

        if (highScoreText != null)
        {
            highScoreText.text = "HI: " + offlineHighScore.ToString();
        }
    }
}
