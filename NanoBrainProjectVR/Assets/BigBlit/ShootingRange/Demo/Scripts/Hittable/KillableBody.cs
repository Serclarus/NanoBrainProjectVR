using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BigBlit.ShootingRange
{



    [Serializable]
    public class OnBodyKilledUnityEvent : UnityEvent<KillableBody, IHitInfo> { }

    [Serializable]
    public class OnBodyAliveUnityEvent : UnityEvent<KillableBody> { }


    public class KillableBody : HittableBody
    {
         
#pragma warning disable CS0067
        public static event UnityAction<KillableBody, IHitInfo> sceneOnKilled;
        public static event UnityAction<KillableBody, IHitInfo> sceneOnAlive;
#pragma warning restore CS0067

        [SerializeField] float _health;

        public event UnityAction<KillableBody, IHitInfo> OnBodyKilled {
            add {
                _bodyKilled.AddListener(value);
            }
            remove {
                _bodyKilled.RemoveListener(value);
            }
        }

        public event UnityAction<KillableBody> OnBodyAlive {
            add {
                _bodyAlive.AddListener(value);
            }
            remove {
                _bodyAlive.RemoveListener(value);
            }

        }

        [SerializeField] float _maxHealth;
        public float MaxHealth => _maxHealth;

        [SerializeField] float _bulletDamageFactor;
        public float HitDamageFactor => _bulletDamageFactor;

        public bool IsAlive => _health > 0.0f;
 

        [SerializeField] OnBodyKilledUnityEvent _bodyKilled;
        [SerializeField] OnBodyAliveUnityEvent _bodyAlive;

        protected override void Awake() {
            base.Awake();
      
        }

        private void Start() {
            if (IsAlive)
                _bodyAlive?.Invoke(this);
            else
                _bodyKilled?.Invoke(this, null);
        }

        public void Rewake() {
            if (IsAlive)
                return;

            _health = _maxHealth;
            _bodyAlive?.Invoke(this);
        }


        public override bool OnHit(IHitInfo hitInfo) {
            if (!IsAlive)
                return false;

            Debug.Log("hIT");
            if (base.OnHit(hitInfo)) {
                updateDamage(hitInfo);
                return true;
            }
            return false;
        }

        private void updateDamage(IHitInfo hitInfo) {
           float damage = 1.0f;
            if (hitInfo.Shot.Bullet != null)
                damage = hitInfo.Shot.Bullet.BulletDamage;
            _health = Mathf.Max(0.0f, _health - damage * _bulletDamageFactor);
            if(_health <= Mathf.Epsilon) {
                _health = 0.0f;
                _bodyKilled?.Invoke(this, hitInfo);  
            }
        }

    }





}
