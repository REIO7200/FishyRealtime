using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace FishyRealtime.Samples
{
    public class RoomButton : MonoBehaviour
    {
        private Room room;
        
        public TMP_Text text;
        public TMP_Text playersText;

        public void Join()
        {
            FishyRealtime.Instance.JoinRoom(room);
        }

        public void Init(Room room)
        {
            this.room = room;
            text.text = room.name;
            playersText.text = room.playerCount.ToString() + "/" + room.maxPlayers.ToString();
        }
    }
}
