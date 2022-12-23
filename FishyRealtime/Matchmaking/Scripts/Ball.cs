using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;

namespace FishyRealtime.Samples
{
    public class Ball : NetworkBehaviour
    {
        Rigidbody rb;

        public float velocity;

        public override void OnStartServer()
        {
            base.OnStartServer();
            Vector3 dir = new Vector3(Random.Range(0.0f, 1.0f), 0.0f, Random.Range(0.0f, 1.0f)).normalized;
            rb = GetComponent<Rigidbody>();
            rb.velocity = dir * velocity;
        }

        void FixedUpdate()
        {
            if (base.IsServer)
            {
                Vector3 dir = rb.velocity.normalized;
                rb.velocity = dir * velocity;
            }
        }
    }
}
