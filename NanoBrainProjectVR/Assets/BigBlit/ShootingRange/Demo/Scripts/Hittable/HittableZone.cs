using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigBlit.ShootingRange
{

    public class HittableZone :  IHittableZone
    {

        private List<IHitInfo> _hits = new List<IHitInfo>();
        public IEnumerable<IHitInfo> Hits => _hits;
       
        [SerializeField] string _name;
        public string Name => _name;

        private IHittablePart _part;

        public IHittablePart Part {
            get => _part;
            set => _part = value;
        }

        public int CountHits => _hits.Count;

        public void ClearHits() {
            foreach(var hit in _hits) {
                if (hit.Shot.Bullet != null)
                    hit.Shot.Bullet.Clear();
            }
            _hits.Clear();
        }

        public virtual void Hit(IHitInfo hitInfo) {
            _hits.Add(hitInfo);
        }

    }
}
