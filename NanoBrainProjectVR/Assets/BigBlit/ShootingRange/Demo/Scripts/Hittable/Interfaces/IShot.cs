using UnityEngine;

namespace BigBlit.ShootingRange
{
    public interface IShot
    {
        /**
          *  Shooter is an object that initialized the shot, ex. the Player.
          **/
        IShooter Shooter { get; }
        IWeapon Weapon { get; }
        IBullet Bullet { get; }
        float ShotTime { get; }
        Vector3 Position { get; }
    }
}
