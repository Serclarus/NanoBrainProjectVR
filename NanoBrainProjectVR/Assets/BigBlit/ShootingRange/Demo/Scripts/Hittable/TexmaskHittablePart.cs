using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigBlit.ShootingRange
{
   
    public class TexmaskHittablePart : HittablePart
    {      
        [SerializeField] Texture2D _texMask = null;

        public enum TexmaskUV { UV0, UV1, UV2 };

        [SerializeField] TexmaskUV _texmaskUV;

        [Serializable]
        class TextMaskHittableZone : HittableZone
        {
            [SerializeField] float _maskId;

            public float MaskId => _maskId;
        }
        public override IEnumerable<IHittableZone> Zones => _zones;

        [SerializeField]
        List<TextMaskHittableZone> _zones = new List<TextMaskHittableZone>();

        protected override IHittableZone GetHittableZone(IHit hit) {
            float distance = Mathf.Infinity;
            IHittableZone result = null;

            float v = getMaskValue(getUVCoords(hit));

            foreach (var zone in _zones) {
                float d = Mathf.Abs(v - zone.MaskId);
                if (d < distance) {
                    distance = d;
                    result = zone;
                }
            }

            return result;

        }
        float getMaskValue(Vector2 uv) {
            if (_texMask == null)
                return Mathf.Infinity;

            return _texMask.GetPixel((int)(uv.x * _texMask.width), (int)(uv.y * _texMask.height)).r;
        }

        Vector2 getUVCoords(IHit hit) {
            return _texmaskUV == TexmaskUV.UV0 ? hit.UV0 : _texmaskUV == TexmaskUV.UV1 ? hit.UV1 : hit.UV2;
        }
    }
}
