using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BigBlit.ShootingRange
{
    public class KillableBodyZoneWaker : MonoBehaviour
    {
        [SerializeField] List<KillableBody> _bodies = new List<KillableBody>();

        [SerializeField] float _delay;
        [SerializeField] float _randomDelay;

        private float _age;
        private bool _woken;
        private float _rd;

        private void Awake() {
            _rd = UnityEngine.Random.Range(0.0f, _randomDelay);
        }

        private void OnTriggerStay(Collider other) {
            if (_woken)
                return;

            _age += Time.deltaTime;
            if (_age < _delay + _rd)
                return;
            _age = 0.0f;

            foreach (var body in _bodies) {
                if (!body.IsAlive)
                    body.Rewake();
            }
            _woken = true;




        }


    }





}
