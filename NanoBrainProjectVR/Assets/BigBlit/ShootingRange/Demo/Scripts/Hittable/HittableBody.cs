using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BigBlit.ShootingRange
{


    [Serializable]
    public class OnBodyHitUnityEvent : UnityEvent<IHittableBody, IHitInfo> { }

    public class HittableBody : MonoBehaviour, IHittableBody
    {

        public static event UnityAction<IHittableBody, IHitInfo> sceneOnBodyHit;

        public event UnityAction<IHittableBody, IHitInfo> OnBodyHit {
            add {
                _bodyHit.AddListener(value);
            }
            remove {
                _bodyHit.RemoveListener(value);
            }
        }

        [SerializeField] OnBodyHitUnityEvent _bodyHit;

        private List<IHittablePart> _parts = new List<IHittablePart>();

        public IEnumerable<IHittablePart> Parts => _parts;

        public GameObject GameObject => this.gameObject;

        public MonoBehaviour Component => this;

        public IEnumerable<IHitInfo> Hits {
            get {
                List<IHitInfo> hits = new List<IHitInfo>();
                foreach (var part in Parts) {
                    hits.AddRange(part.Hits);
                }
                return hits;
            }
        }

        public int CountHits {
            get {
                int hits = 0;
                foreach (var part in Parts)
                    hits += part.CountHits;
                return hits;
            }
        }

        protected virtual void Awake() {
            GetComponents<IHittablePart>(_parts);
            GetComponentsInChildren<IHittablePart>(_parts);
           
            foreach (var part in _parts)
                if(part != null)
                    part.Body = this;
        }

        protected virtual void OnDestroy() {
            foreach (var part in _parts) {
                if(part != null)
                  part.Body = null;
            }
        }

        public void ClearHits() {
            foreach (var part in _parts) {
                if (part != null)
                    part.ClearHits();
            }
        }

        public virtual bool OnHit(IHitInfo hitInfo) {
            _bodyHit?.Invoke(this, hitInfo);
            sceneOnBodyHit?.Invoke(this, hitInfo);
            return true;
        }
    }





}
