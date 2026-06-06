using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class KnockdownTarget : NetworkBehaviour
{
    [Header("Target Setup")]
    [Tooltip("The specific child object/pivot that should fall backward. If left empty, it rotates the object this script is attached to.")]
    public Transform pivotTransform;

    [Header("Knockdown Settings")]
    [Tooltip("How many degrees backward the target should fall (along the local X axis).")]
    public float knockdownAngle = 90f;
    [Tooltip("How fast the target falls down.")]
    public float fallSpeed = 10f;
    [Tooltip("How many seconds the target stays down before popping back up.")]
    public float timeToStandUp = 3f;
    [Tooltip("If true, the target automatically pops back up. If false, it stays down forever!")]
    public bool autoRestore = true;
    [Tooltip("How fast the target pops back up.")]
    public float restoreSpeed = 5f;

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip restoreSound;

    // Network variable to sync the down state automatically across all players!
    public NetworkVariable<bool> isDown = new NetworkVariable<bool>(false);

    private Quaternion originalRotation;
    private Quaternion knockedRotation;
    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (pivotTransform == null)
            pivotTransform = transform;

        originalRotation = pivotTransform.localRotation;
        knockedRotation = originalRotation * Quaternion.Euler(knockdownAngle, 0, 0);
    }

    public override void OnNetworkSpawn()
    {
        // Whenever the server changes the isDown value, this triggers on ALL clients instantly
        isDown.OnValueChanged += OnTargetStateChanged;

        // If a player joins late and the target is already down, snap it down instantly
        if (isDown.Value)
        {
            pivotTransform.localRotation = knockedRotation;
        }
    }

    public override void OnNetworkDespawn()
    {
        isDown.OnValueChanged -= OnTargetStateChanged;
    }

    // Global event for the Training Grounds Manager to track score
    public static event System.Action OnTargetKnockedDown;

    // Call this method from your HittableSurface script's Unity Event!
    public void Knockdown()
    {
        if (isDown.Value) return; // Already down
        
        OnTargetKnockedDown?.Invoke();

        // If we are playing offline without a server, just do it locally
        if (!IsSpawned)
        {
            StartCoroutine(OfflineSequence());
            return;
        }

        // Send a message to the server telling it we shot the target
        KnockdownServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void KnockdownServerRpc()
    {
        if (isDown.Value) return;

        // The server officially marks it as down, which syncs to all clients!
        isDown.Value = true;
        
        // The server starts a timer to pop it back up
        StartCoroutine(ServerRestoreTimer());
    }

    private IEnumerator ServerRestoreTimer()
    {
        yield return new WaitForSeconds(timeToStandUp);
        if (autoRestore)
        {
            isDown.Value = false; // Syncs to all clients again to stand up
        }
    }

    // This handles the fallback if the game is completely offline
    public bool isLocallyDown = false;

    private IEnumerator OfflineSequence()
    {
        isLocallyDown = true;
        OnTargetStateChanged(false, true);
        yield return new WaitForSeconds(timeToStandUp);
        if (autoRestore)
        {
            OnTargetStateChanged(true, false);
        }
        isLocallyDown = false;
    }

    private void OnTargetStateChanged(bool previousValue, bool newValue)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);

        if (newValue == true) // Falling down
        {
            if (audioSource != null && hitSound != null) audioSource.PlayOneShot(hitSound);
            animationCoroutine = StartCoroutine(AnimateRotation(knockedRotation, fallSpeed));
        }
        else // Popping back up
        {
            if (audioSource != null && restoreSound != null) audioSource.PlayOneShot(restoreSound);
            animationCoroutine = StartCoroutine(AnimateRotation(originalRotation, restoreSpeed));
        }
    }

    private IEnumerator AnimateRotation(Quaternion targetRotation, float speed)
    {
        while (Quaternion.Angle(pivotTransform.localRotation, targetRotation) > 0.1f)
        {
            pivotTransform.localRotation = Quaternion.Slerp(pivotTransform.localRotation, targetRotation, Time.deltaTime * speed);
            yield return null;
        }
        pivotTransform.localRotation = targetRotation;
    }
}
