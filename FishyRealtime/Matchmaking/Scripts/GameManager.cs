using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object.Synchronizing;

namespace FishyRealtime.Samples
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        public GameObject loadingScreen;

        public GameObject buttonPrefab;

        public GameObject roomListPanel;

        public GameObject loginPanel;

        //Cant join rooms if we are not connected to the master server
        bool connectedToMaster = false;

        Room room = new Room()
        {
            isPublic = true,
            open = true
        };

        RoomFilter customOptions = new RoomFilter()
        {
            gameMode = "Blue"
        };

        //If both strings are null, no filter will be applied
        RoomFilter listFilter = new RoomFilter();

        string joinRoomName;

        public SyncDictionary<string, int> scores = new SyncDictionary<string, int>();

        // Start is called before the first frame update
        void Start()
        {
            if (Instance != null) Destroy(Instance);
            Instance = this;
            if (FishyRealtime.isConnectedToMaster)
            {
                connectedToMaster = true;
                return;
            }
            loginPanel.SetActive(true);
            loadingScreen.SetActive(true);
            FishyRealtime.Instance.ConnectedToMaster += FishyRealtime_ConnectedToMaster;
        }


        private void FishyRealtime_ConnectedToMaster(object sender, System.EventArgs e)
        {
            connectedToMaster = true;
            loadingScreen.SetActive(false);
            //SearchForRooms();
        }

        // Update is called once per frame
        void Update()
        {

        }

        #region Join Random
        public void JoinRandom()
        {
            if (!connectedToMaster) return;
            FishyRealtime.Instance.JoinRandomRoom(true);
        }

        #endregion

        #region Create Room

        public void SetRoomName(string roomName)
        {
            room.name = roomName;
        }

        public void SetMaxPlayers(string maxPlayers)
        {
            room.maxPlayers = byte.Parse(maxPlayers);
        }

        public void SetIsPublic(bool value)
        {
            room.isPublic = value;
        }

        public void CreateRoom()
        {
            FishyRealtime.Instance.CreateRoom(room, customOptions);
        }

        public void SetGamemode(int index)
        {
            if (index == 0) customOptions.gameMode = "Blue";
            else if (index == 1) customOptions.gameMode = "Red";
        }
        #endregion

        #region Room List

        public void SearchForRooms()
        {
            //Skip the text object
            for (int i = 1; i < roomListPanel.transform.childCount; i++)
            {
                Destroy(roomListPanel.transform.GetChild(i).gameObject);
            }

            //If you want to search for all rooms, just put FishyRealtime.Instance.GetRoomList()
            Room[] rooms = FishyRealtime.Instance.GetRoomList(listFilter);
            for (int i = 0; i < Mathf.Min(rooms.Length, 8); i++)
            {
                RoomButton button = Instantiate(buttonPrefab, roomListPanel.transform).GetComponent<RoomButton>();
                button.Init(rooms[i]);
            }
        }

        public void SetListGamemode(int index)
        {
            if (index == 0) listFilter.gameMode = null;
            else if (index == 1) listFilter.gameMode = "Blue";
            else if (index == 2) listFilter.gameMode = "Red";
        }

        #endregion

        void OnDestroy()
        {
            FishyRealtime.Instance.ConnectedToMaster -= FishyRealtime_ConnectedToMaster;
        }

        #region Join Room

        public void SetJoinRoomName(string roomName)
        {
            joinRoomName = roomName;
        }

        public void JoinRoom()
        {
            FishyRealtime.Instance.JoinRoom(joinRoomName);
        }
        #endregion

        #region Username

        public void SetUsername(string username)
        {
            FishyRealtime.Instance.playerName = username;
        }

        public void Login()
        {
            if (FishyRealtime.Instance.playerName == null)
            {
                Debug.LogError("Username is null!");
                return;
            }
            loginPanel.SetActive(false);
        }

        #endregion
    }
}