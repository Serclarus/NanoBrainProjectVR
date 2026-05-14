using UnityEngine;

namespace BigBlit.ShootingRange
{
 

    public class HitInfo : IHitInfo
    {
        private IShot _shot;
        private IHit _hit;
        private IHittableZone _hittableZone;

        public IShot Shot => _shot;
        public IHit Hit => _hit;
        public IHittableZone HittableZone => _hittableZone;

        public float Distance => Vector3.Distance(_hit.Position, _shot.Position);

        public HitInfo(IShot shot, IHit hit, IHittableZone hittableZone) {
            _shot = shot;
            _hit = hit;
            _hittableZone = hittableZone;
        }
    }





}
