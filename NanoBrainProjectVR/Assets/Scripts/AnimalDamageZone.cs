using UnityEngine;

public class AnimalDamageZone : MonoBehaviour
{
    public enum ZoneType { Body, Head, Heart }
    
    [Header("Zone Configuration")]
    [Tooltip("The type of body part this collider represents.")]
    public ZoneType zoneType = ZoneType.Body;
    
    [Tooltip("The main health script for this animal.")]
    public AnimalHealth animalHealth;

    // Optional event if we want to spawn specific blood effects per zone
    public static event System.Action OnAnimalHit;

    public void ApplyDamage(float baseDamage)
    {
        if (animalHealth == null || animalHealth.isDead) return;

        OnAnimalHit?.Invoke();

        // Calculate Multiplier
        float multiplier = 1.0f;
        
        switch (zoneType)
        {
            case ZoneType.Head:
            case ZoneType.Heart:
                // Instant Kill!
                animalHealth.TakeDamage(animalHealth.maxHealth * 10f);
                break;
            case ZoneType.Body:
            default:
                // Normal bullet damage
                animalHealth.TakeDamage(baseDamage);
                break;
        }
    }
}
