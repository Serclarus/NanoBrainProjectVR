using UnityEngine;
using UnityEngine.Events;

public class AnimalHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;
    private float currentHealth;

    public bool isDead { get; private set; } = false;

    [Header("Events")]
    [Tooltip("Fired immediately when the animal takes damage (useful for AI reactions).")]
    public UnityEvent onDamageTaken;
    
    [Tooltip("Fired when health reaches 0.")]
    public UnityEvent onDeathEvent;

    private void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        onDamageTaken?.Invoke();

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
            onDeathEvent?.Invoke();
        }
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
}
