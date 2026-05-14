using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BigBlit.ShootingRange
{

    public interface IBehaviourComponent {
        GameObject GameObject { get; }
        MonoBehaviour Component { get; }
    
    }
    
    public interface IHittableBody : IBehaviourComponent
    {
        event UnityAction<IHittableBody, IHitInfo> OnBodyHit;

        IEnumerable<IHittablePart> Parts { get; }
        IEnumerable<IHitInfo> Hits { get; }
        int CountHits { get; }
        void ClearHits();
        bool OnHit(IHitInfo hitInfo);
    }
}
