using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HitEffectSetup
{
    public SurfaceType surfaceType;
    public GameObject effectPrefab;
}

public class HitEffectPoolManager : MonoBehaviour
{
    public static HitEffectPoolManager Instance { get; private set; }

    [Header("Pool Settings")]
    [Tooltip("Map different surface types to different particle prefabs")]
    public List<HitEffectSetup> hitEffectSetups;
    
    [Tooltip("How many of EACH effect type should exist in memory?")]
    public int poolSizePerType = 20;
    
    public float effectDuration = 7f; 

    // Dictionary mapping SurfaceType to its queue of pooled GameObjects
    // Now acts as a cyclic buffer where the oldest effect is dequeued and immediately enqueued at the end
    private Dictionary<SurfaceType, Queue<GameObject>> poolDictionary;
    private Dictionary<GameObject, Coroutine> activeCoroutines;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        InitializePools();
    }

    private void InitializePools()
    {
        poolDictionary = new Dictionary<SurfaceType, Queue<GameObject>>();
        activeCoroutines = new Dictionary<GameObject, Coroutine>();

        foreach (var setup in hitEffectSetups)
        {
            if (setup.effectPrefab == null) continue;

            Queue<GameObject> objectPool = new Queue<GameObject>();

            for (int i = 0; i < poolSizePerType; i++)
            {
                GameObject effect = Instantiate(setup.effectPrefab, transform);
                effect.SetActive(false);
                objectPool.Enqueue(effect);
            }

            poolDictionary.Add(setup.surfaceType, objectPool);
        }
    }

    public void SpawnHitEffect(Vector3 position, Vector3 normal, SurfaceType type, Transform parent = null)
    {
        // If we shoot something that has no effect set up, fallback to Default
        if (!poolDictionary.ContainsKey(type))
        {
            if (poolDictionary.ContainsKey(SurfaceType.Default))
                type = SurfaceType.Default;
            else
                return; // Nothing to spawn
        }

        // Dequeue the oldest effect, no matter if it's currently active or not
        if (poolDictionary[type].Count > 0)
        {
            GameObject effect = poolDictionary[type].Dequeue();
            
            // Stop the previous disable coroutine if one is running
            if (activeCoroutines.TryGetValue(effect, out Coroutine existingCoroutine) && existingCoroutine != null)
            {
                StopCoroutine(existingCoroutine);
            }
            
            // Disable it to properly reset particle systems or trail renderers before moving
            effect.SetActive(false);
            
            if (parent != null)
            {
                effect.transform.SetParent(parent, true);
            }
            else
            {
                effect.transform.SetParent(transform, true);
            }

            effect.transform.position = position;
            effect.transform.rotation = Quaternion.LookRotation(normal);
            effect.SetActive(true);

            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();

            // Start a new coroutine to clear the effect eventually and track it
            Coroutine newCoroutine = StartCoroutine(ReturnToPoolAfterTime(effect, effectDuration));
            activeCoroutines[effect] = newCoroutine;
            
            // Re-queue the effect to the back so it can be reused once all other effects have been cycled
            poolDictionary[type].Enqueue(effect);
        }
    }

    private IEnumerator ReturnToPoolAfterTime(GameObject effect, float time)
    {
        yield return new WaitForSeconds(time);
        
        if (effect != null)
            effect.SetActive(false);
            
        // We do not enqueue here anymore because it remains in the queue sequence permanently
    }
}
