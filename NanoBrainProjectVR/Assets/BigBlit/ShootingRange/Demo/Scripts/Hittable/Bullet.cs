using UnityEngine;

namespace BigBlit.ShootingRange
{
    public class Bullet : MonoBehaviour, IBullet
    {
        [SerializeField] float _bulletDamage;
        public float BulletDamage => _bulletDamage;
       
        public void Clear() {
            gameObject.SetActive(false);
        }
    }
}
