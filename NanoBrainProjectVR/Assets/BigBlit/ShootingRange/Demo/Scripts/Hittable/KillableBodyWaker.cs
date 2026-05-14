using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BigBlit.ShootingRange
{



    public class KillableBodyWaker : MonoBehaviour
    {
        [SerializeField] float _delay;
        [SerializeField] float _randomDelay;
        [SerializeField] bool _shouldWake;

        [SerializeField] bool _clearHits;

        KillableBody _body;

        public bool ShouldWake {
            get => _shouldWake;
            set => _shouldWake = value;
        }

        public void Wake(bool shouldWake = true) {
            wake();
        }

        private void Awake() {
            _body = GetComponent<KillableBody>();
        }

        private void OnEnable() {
            if (_body != null)
                _body.OnBodyKilled += onBodyKilled;
        }

        private void OnDisable() {
            if (_body != null)
                _body.OnBodyKilled -= onBodyKilled;
        }


        void onBodyKilled(KillableBody body, IHitInfo hitInfo) {
            wake();
        }

        void wake() {
            StartCoroutine(wakeDelayed(_delay + UnityEngine.Random.Range(0.0f, _randomDelay)));
        }

        IEnumerator wakeDelayed(float secs) {
            yield return new WaitForSeconds(secs);
            if (_body != null) {
                if (_clearHits)
                    _body.ClearHits();
                _body.Rewake();
            }
        }

    }





}
