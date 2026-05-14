using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigBlit.ShootingRange
{

    public class TaggedHittablePart : HittablePart
    {

        [Serializable]
        class TaggedHittableZone : HittableZone
        {
            [SerializeField] string _tag;

            public string Tag => _tag;
        }

        public override IEnumerable<IHittableZone> Zones => _zones;

        [SerializeField]
        List<TaggedHittableZone> _zones = new List<TaggedHittableZone>();

        protected override IHittableZone GetHittableZone(IHit hit) {
            if (hit.Collider != null && string.IsNullOrEmpty(hit.Collider.tag)) {
                foreach (var zone in _zones) {
                    if (string.Equals( zone.Tag, hit.Collider.tag))
                        return zone;
                }
            }

            return null;
        }
    }
}
