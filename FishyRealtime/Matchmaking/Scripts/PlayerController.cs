using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;

namespace FishyRealtime.Samples
{
    public class PlayerController : NetworkBehaviour
    {
        public new MeshRenderer renderer;
        public TMP_Text usernameText;

        public Material blue, red;
        [SerializeField]
        Camera cam;

        public float moveSpeed;

        Vector3 moveDirection;

        [SyncVar(OnChange = nameof(UpdateScore))]
        public int score = 0;

        public override void OnStartClient()
        {
            base.OnStartClient();
            usernameText.text = FishyRealtime.Instance.GetPlayerUsername(OwnerId) + " Score: 0";


            if (base.IsOwner)
            {
                if (FishyRealtime.Instance.GetRoomFilter().gameMode == "Blue") renderer.material = blue;
                else renderer.material = red;
            }
            else
            {
                if (FishyRealtime.Instance.GetRoomFilter().gameMode == "Blue") renderer.material = red;
                else renderer.material = blue;
                Destroy(cam.gameObject);
            }
        }

        void Update()
        {
            if (!base.IsOwner) return;
            float angle = -Input.GetAxis("Horizontal") * moveSpeed;
            transform.Rotate(Vector3.up, angle * Time.deltaTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if(collision.collider.CompareTag("Ball") && base.IsServer)
            {
                Debug.Log("Hit!");
                score++;
                GameManager.Instance.scores[FishyRealtime.Instance.GetPlayerUsername(OwnerId)] = score;
            }
        }

        void UpdateScore(int prev, int next, bool asServer)
        {
            usernameText.text = FishyRealtime.Instance.GetPlayerUsername(OwnerId) + " Score: " + next.ToString();
        }
    }
}
