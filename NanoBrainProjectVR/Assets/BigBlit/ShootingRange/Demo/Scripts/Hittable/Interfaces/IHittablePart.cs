using System.Collections.Generic;

namespace BigBlit.ShootingRange
{
    public interface IHittablePart : IHittable, IBehaviourComponent
    {
        IHittableBody Body { get; set;  }
        string Name { get; }
        IEnumerable<IHittableZone> Zones { get; }

        void ClearHits();
        IEnumerable<IHitInfo> Hits { get; }
        int CountHits { get; }
        
    }





}
