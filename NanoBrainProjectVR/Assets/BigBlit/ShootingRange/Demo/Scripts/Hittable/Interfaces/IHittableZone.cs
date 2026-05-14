using System.Collections.Generic;

namespace BigBlit.ShootingRange
{
    public interface IHittableZone
    {
        IEnumerable<IHitInfo> Hits { get; }
        int CountHits { get; }

        string Name { get; }
         IHittablePart Part { get; set;  }

        void ClearHits();
        void Hit(IHitInfo hitInfo);

    }





}
