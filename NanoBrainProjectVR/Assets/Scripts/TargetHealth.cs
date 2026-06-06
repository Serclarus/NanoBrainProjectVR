using UnityEngine;
using UnityEngine.Events;

public class TargetHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;
    
    public bool isDead { get; private set; } = false;
    public int timeOfDeathFrame { get; private set; } = -1;

    [Header("Events")]
    [Tooltip("Fired when health reaches 0. Hook this up to KnockdownTarget.Knockdown()")]
    public UnityEvent onDeathEvent;

    private void OnEnable()
    {
        // Reset health every time the target pops back up or spawns
        currentHealth = maxHealth;
        isDead = false;
        timeOfDeathFrame = -1;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
            timeOfDeathFrame = Time.frameCount;
            onDeathEvent?.Invoke();
        }
    }
}
