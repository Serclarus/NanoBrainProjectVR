using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace BigBlit.ShootingRange
{
    public class WakeThemAll : MonoBehaviour
    {
        private void Start() {
           var bodies = FindObjectsOfType<KillableBody>();
            foreach(var body in bodies) {
                body.Rewake();
            }
        }

    }





}
