using UnityEngine;

public class TargetDamageZone : MonoBehaviour
{
    public enum ZoneType { Body, Head }
    
    [Header("Zone Configuration")]
    public ZoneType zoneType = ZoneType.Body;
    public TargetHealth targetHealth;

    public static event System.Action OnTargetHit;

    public void ApplyDamage(float baseDamage)
    {
        if (targetHealth == null) return;

        // If the target is ALREADY dead, we only count it as a hit if the bullet struck 
        // on the exact same frame it died. (This perfectly handles the Shotgun's 8 pellets).
        if (targetHealth.isDead)
        {
            if (Time.frameCount == targetHealth.timeOfDeathFrame)
            {
                OnTargetHit?.Invoke();
            }
            return; // Don't deal damage to a dead target
        }

        // It was a valid hit on a living target!
        OnTargetHit?.Invoke();

        // Calculate Multiplier
        float multiplier = (zoneType == ZoneType.Head) ? 3.0f : 1.0f;
        
        targetHealth.TakeDamage(baseDamage * multiplier);
    }
}
