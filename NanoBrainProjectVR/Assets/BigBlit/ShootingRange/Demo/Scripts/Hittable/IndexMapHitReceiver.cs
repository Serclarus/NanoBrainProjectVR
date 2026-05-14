//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//namespace BigBlit.ShootingRange
//{
//    public class IndexMapHitReceiver : RaycastHitReceiver
//    {
//        [SerializeField] Texture2D _indexMap;

//        enum TexCoords { UV0, UV1, UV2 };
//        [SerializeField] TexCoords _indexMapUV;

//        float getValueFromMap(RaycastHit raycastHit) {
//            if (_indexMap == null)
//                return 0.0f;

//            Vector2 uv = _indexMapUV == TexCoords.UV0 ? raycastHit.textureCoord :
//                _indexMapUV == TexCoords.UV1 ? raycastHit.lightmapCoord : raycastHit.textureCoord2;
//            return _indexMap.GetPixel((int)(uv.x * _indexMap.width), (int)(uv.y * _indexMap.height)).r;

//        }
//        public override void Hit(RaycastHit raycastHit, IShot shot) {
//            if(_indexMap == null) {
//                base.Hit(raycastHit, shot);
//                return;
//            }

//            float id = getValueFromMap(raycastHit);
//            Debug.Log(id);
//            float dist = Mathf.Infinity;
//            IHittablePart processor = null;

//            foreach(var p in _processors) {
//                float d = Mathf.Abs(p.PartId        IHit Hit { get; } - id);
//                if(d < dist) {
//                    dist = d;
//                    processor = p;
//                }
//            }

//            processor.ProcessHit(new Hit(Time.time, raycastHit.normal), shot);
//        }
//    }





//}
