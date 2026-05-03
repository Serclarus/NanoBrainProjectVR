using UnityEngine;

public class RangeStarter: MonoBehaviour

{
    [SerializeField] private ShootingRangeManager rangeManager;

    private void OnTriggerEnter(Collider other)
    {
        rangeManager.StartRange();
    }
}
