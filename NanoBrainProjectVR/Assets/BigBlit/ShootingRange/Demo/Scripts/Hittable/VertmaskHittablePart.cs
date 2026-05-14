using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigBlit.ShootingRange
{

    public class VertmaskHittablePart : HittablePart
    {

        [Serializable]
        class VertmaskHittableZone : HittableZone
        {
            [SerializeField] float _maskId;

            public float MaskId => _maskId;
        }

        public override IEnumerable<IHittableZone> Zones => _zones;

        [SerializeField]
        List<VertmaskHittableZone> _zones = new List<VertmaskHittableZone>();

        Collider _collider = null;
        Mesh _mesh = null;


        protected override IHittableZone GetHittableZone(IHit hit) {
            if (hit.Collider == null)
                return null;
            if(hit.Collider != _collider) {
                var meshCollider = hit.Collider.GetComponent<MeshCollider>();
                if (meshCollider == null || meshCollider.sharedMesh == null)
                    return null;
                _mesh = meshCollider.sharedMesh;
            }

            int vid = hit.TriangleId * 3;

            if (_mesh.colors == null)
                return null;

            IHittableZone result = null;
           float v = _mesh.colors[_mesh.triangles[hit.TriangleId * 3]].r;
            float distance = Mathf.Infinity;
            foreach (var zone in _zones) {
                float d = Mathf.Abs(v - zone.MaskId);
                if (d < distance) {
                    distance = d;
                    result = zone;
                }
            }

            return result;
        }
    }
}
