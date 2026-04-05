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
    private Dictionary<SurfaceType, Queue<GameObject>> poolDictionary;

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

    public void SpawnHitEffect(Vector3 position, Vector3 normal, SurfaceType type)
    {
        // If we shoot something that has no effect set up, fallback to Default
        if (!poolDictionary.ContainsKey(type))
        {
            if (poolDictionary.ContainsKey(SurfaceType.Default))
                type = SurfaceType.Default;
            else
                return; // Nothing to spawn
        }

        if (poolDictionary[type].Count > 0)
        {
            GameObject effect = poolDictionary[type].Dequeue();
            
            effect.transform.position = position;
            effect.transform.rotation = Quaternion.LookRotation(normal);
            effect.SetActive(true);

            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();

            StartCoroutine(ReturnToPoolAfterTime(effect, type, effectDuration));
        }
        else
        {
            Debug.LogWarning($"Hit Effect Pool for {type} is empty!");
        }
    }

    private IEnumerator ReturnToPoolAfterTime(GameObject effect, SurfaceType type, float time)
    {
        yield return new WaitForSeconds(time);
        
        effect.SetActive(false);
        poolDictionary[type].Enqueue(effect);
    }
}
