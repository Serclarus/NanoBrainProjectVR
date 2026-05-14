using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BigBlit.ShootingRange
{

    public class Shot : IShot
    {

        private IShooter _shooter;
        private IWeapon _weapon;
        private IBullet _bullet;
        private float _shotTime;
        private Vector3 _shotPosition;

        public IShooter Shooter => _shooter;
        public IWeapon Weapon => _weapon;
        public IBullet Bullet => _bullet;
        public float ShotTime => _shotTime;
        public Vector3 Position => _shotPosition;


        public Shot(IShooter shooter, IWeapon weapon, IBullet bullet, float shotTime, Vector3 shotPosition) {
            _shooter = shooter;
            _weapon = weapon;
            _bullet = bullet;
            _shotTime = shotTime;
            _shotPosition = shotPosition;

        }
    }
}
