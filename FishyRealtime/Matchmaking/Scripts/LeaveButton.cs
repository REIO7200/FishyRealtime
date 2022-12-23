using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FishyRealtime.Samples
{
    public class LeaveButton : MonoBehaviour
    {
        public void LeaveRoom()
        {
            FishyRealtime.Instance.LeaveRoom();
        }   
    }
}
