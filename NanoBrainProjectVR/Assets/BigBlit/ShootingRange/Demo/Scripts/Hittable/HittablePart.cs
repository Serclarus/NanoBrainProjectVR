using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigBlit.ShootingRange
{

    public abstract class HittablePart : MonoBehaviour, IHittablePart
    {
        public static event OnHit sceneOnHit;
        public event OnHit onHit;

        private IHittableBody _body;
        public IHittableBody Body {
            get => _body;
            set => _body = value;
        }

        public GameObject GameObject => this.gameObject;
        public MonoBehaviour Component => this;

        [SerializeField] private string _name;
        public string Name => _name;

        void Awake() {
            foreach (var zone in Zones)
                zone.Part = this;
        }

        public abstract IEnumerable<IHittableZone> Zones { get; }

        public void ClearHits() {
            foreach (var zone in Zones) {
                zone.ClearHits();
            }
        }

        public IEnumerable<IHitInfo> Hits {
            get {
                List<IHitInfo> hits = new List<IHitInfo>();
                foreach(var zone in Zones) {
                    hits.AddRange(zone.Hits);
                }
                return hits;
            }
        }

        public int CountHits {
            get {
                int hits = 0;
                foreach (var zone in Zones)
                    hits += zone.CountHits;
                return hits;
            }
        }

        protected abstract IHittableZone GetHittableZone(IHit hit);

        public bool Hit(IShot shot, IHit hit) {

            var zone = GetHittableZone(hit);
            if (zone == null)
                return false;

            IHitInfo hitInfo = new HitInfo(shot, hit, zone);

            if (_body != null) {
                if (!_body.OnHit(hitInfo))
                    return false;
            }

            zone.Hit(hitInfo);

            onHit?.Invoke(hitInfo);
            sceneOnHit?.Invoke(hitInfo);
            return true;
        }


    }





}
