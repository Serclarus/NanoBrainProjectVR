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
    public int poolSizePerType = 100;
    
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
                // Spawn at the pool manager's location.
                GameObject effect = Instantiate(setup.effectPrefab, transform.position, Quaternion.identity, transform);
                if (i == 0)
                {
                    // WARM UP SHADERS & PARTICLE BUFFERS ON SCENE LOAD:
                    effect.SetActive(true);
                    ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var p in particles)
                    {
                        p.Emit(1);
                        p.Clear(true);
                    }
                }
                effect.SetActive(false); // Disable after warming up
                
                objectPool.Enqueue(effect);
            }

            poolDictionary.Add(setup.surfaceType, objectPool);
        }
    }

    public void SpawnHitEffect(Vector3 position, Vector3 normal, SurfaceType type, Transform parent = null)
    {
        if (!poolDictionary.ContainsKey(type))
        {
            if (poolDictionary.ContainsKey(SurfaceType.Default))
                type = SurfaceType.Default;
            else
                return; 
        }

        if (poolDictionary[type].Count > 0)
        {
            GameObject effect = poolDictionary[type].Dequeue();
            
            if (activeCoroutines.TryGetValue(effect, out Coroutine existingCoroutine) && existingCoroutine != null)
            {
                StopCoroutine(existingCoroutine);
            }
            
            effect.SetActive(false);
            effect.transform.SetParent(parent != null ? parent : transform, true);
            
            // Pull out by 0.01f to prevent z-fighting
            effect.transform.position = position + (normal * 0.01f);
            effect.transform.rotation = Quaternion.LookRotation(normal);

            ParticleSystem[] allParticles = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in allParticles)
            {
                p.Clear(true);
            }

            effect.SetActive(true);

            foreach (var p in allParticles)
            {
                p.Play(true);
            }

            Coroutine newCoroutine = StartCoroutine(ReturnToPoolAfterTime(effect, effectDuration));
            activeCoroutines[effect] = newCoroutine;
            
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
