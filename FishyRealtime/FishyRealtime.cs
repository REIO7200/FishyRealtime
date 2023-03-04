using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Transporting;
using System;
using Photon.Realtime;
using ExitGames.Client.Photon;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishyRealtime
{
    /// <summary>
    /// A room
    /// </summary>
    public struct Room
    {
        /// <summary>
        /// The name of the room
        /// </summary>
        public string name;
        /// <summary>
        /// Should the room be visible for all? (IDK if this works)
        /// </summary>
        public bool isPublic;
        /// <summary>
        /// Can clients join this room?
        /// </summary>
        public bool open;
        /// <summary>
        /// Max number of players
        /// </summary>
        public byte maxPlayers;
        /// <summary>
        /// How much players are on this room, changing this wont do nothing to the room itself
        /// </summary>
        public int playerCount;
    }

    /// <summary>
    /// Used to filter rooms on matchmaking
    /// </summary>
    public struct RoomFilter
    {
        public string mapName;
        public string gameMode;
    }

    public enum Region : byte
    {
        Asia = 0,
        Australia = 1,
        CanadaEast = 2,
        Europe = 3,
        India = 4,
        Japan = 5,
        Russia = 6,
        RussiaEast = 7,
        SouthAfrica = 8,
        SouthAmerica = 9,
        SouthKorea = 10,
        Turkey = 11,
        USAEast = 12,
        USAWest = 13
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class FishyRealtime : Transport, IConnectionCallbacks, IMatchmakingCallbacks, IOnEventCallback, IInRoomCallbacks, ILobbyCallbacks
    {
        private LoadBalancingClient client = new LoadBalancingClient();

        public static FishyRealtime Instance;

        //This is cached to avoid allocations
        private RaiseEventOptions eventOptions = new RaiseEventOptions()
        {
            TargetActors = new int[1]
        };

        [Tooltip("The app id of the photon realtime product")]
        public string photonAppId;

        [Tooltip("The version of the product(Game), only clients with the same version can connect to each other")]
        public string appVersion = "0.0.1";

        [Header("Default room options")]

        [Tooltip("Default max players")]
        public byte maxPlayers = 10;

        [Tooltip("Should this room apper on the room list?")]
        public bool isVisible = true;

        [Tooltip("Can players join this room?")]
        public bool isOpen = true;

        [Tooltip("What is goinng to be used to connect")]
        public ConnectionProtocol connectionProtocol;

        [Tooltip("Region to connect")]
        public Region region;

        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;

        Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();


        public override string GetConnectionAddress(int connectionId)
        {
            return (connectionId + 1).ToString();
        }

        //Not used
        public override void SetClientAddress(string address)
        {

        }

        private void Start()
        {
            //Make sure thre is not any other instances
            if (Instance != null) Destroy(this);
            //Assign the instance
            Instance = this;
            //Connect to master
            ConnectToRegion(region);

            //To handle playmode stop
#if UNITY_EDITOR 
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;

        }

        private void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.ExitingPlayMode) Disconnect();
#endif
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        #region Connection state

        //Some events that FishNet uses
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;

        public override void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
        {
            OnClientConnectionState?.Invoke(connectionStateArgs);
        }

        public override void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs)
        {
            OnServerConnectionState?.Invoke(connectionStateArgs);
        }

        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs)
        {
            OnRemoteConnectionState?.Invoke(connectionStateArgs);
        }

        public override LocalConnectionState GetConnectionState(bool server)
        {
            switch (client.State)
            {
                case ClientState.Joining:
                    return LocalConnectionState.Starting;
                case ClientState.Joined:
                    return LocalConnectionState.Started;
                case ClientState.Leaving:
                    return LocalConnectionState.Stopping;
            }
            return LocalConnectionState.Stopped;
        }

        public override RemoteConnectionState GetConnectionState(int connectionId)
        {
            //I use a try catch, since current room can be null
            try
            {
                //If there is no player, return stopped
                //This should never happen
                if (client.CurrentRoom.Players[connectionId] == null) return RemoteConnectionState.Stopped;
            }
            catch
            {
                //If we are outside a room, return stopped
                return RemoteConnectionState.Stopped;
            }
            //Then check if the player is active
            if (client.CurrentRoom.Players[connectionId].IsInactive)
            {
                return RemoteConnectionState.Stopped;
            }
            else
            {
                return RemoteConnectionState.Started;
            }
        }
        #endregion

        #region Sending and Receiving

        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (!client.InRoom) return;
            //Decide what channel to use
            SendOptions options = channelId == (byte)Channel.Reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;

            //Sometimes the segment isnt large enough
            if ((segment.Array.Length - 1) <= (segment.Offset + segment.Count))
            {
                byte[] arr = segment.Array;
                Array.Resize(ref arr, arr.Length + 1);
                arr[arr.Length - 1] = channelId;
            }
            //If it is, just insert a new byte
            else
            {
                segment.Array[segment.Offset + segment.Count] = channelId;
            }

            segment = new ArraySegment<byte>(segment.Array, segment.Offset, segment.Count + 1);

            //If we are host, 
            if (NetworkManager.IsHost)
            {
                SendHost(segment);
                return;
            }

            //We only want to send data to the master client
            RaiseEventOptions raiseOptions = new RaiseEventOptions()
            {
                Receivers = ReceiverGroup.MasterClient
            };
            //Send the data
            client.OpRaiseEvent(0, segment, raiseOptions, options);
        }

        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (!client.InRoom) return;
            //Decide what channel to use
            SendOptions options = channelId == (byte)Channel.Reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;

            //Sometimes the segment isnt large enough
            if ((segment.Array.Length - 1) <= (segment.Offset + segment.Count))
            {
                byte[] arr = segment.Array;
                Array.Resize(ref arr, arr.Length + 1);
                arr[arr.Length - 1] = channelId;
            }
            //If it is, just insert a new byte
            else
            {
                segment.Array[segment.Offset + segment.Count] = channelId;
            }

            segment = new ArraySegment<byte>(segment.Array, segment.Offset, segment.Count + 1);

            //Set the ID of the connection where we want to send
            eventOptions.TargetActors[0] = connectionId + 1;

            //Send the data
            client.OpRaiseEvent(0, segment, eventOptions, options);
        }

        //Internal FishNet things

        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs)
        {
            OnClientReceivedData?.Invoke(receivedDataArgs);
        }

        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs receivedDataArgs)
        {
            OnServerReceivedData?.Invoke(receivedDataArgs);
        }

        public override void IterateIncoming(bool server)
        {
            client.LoadBalancingPeer.DispatchIncomingCommands();
        }

        public override void IterateOutgoing(bool server)
        {
            client.LoadBalancingPeer.SendOutgoingCommands();
        }

        //Called when a photon event is received
        public void OnEvent(EventData photonEvent)
        {
            //If it is a internal event, nothing to do
            if (photonEvent.Code >= 200) return;
            //Kick event
            if (photonEvent.Code == 1) LeaveRoom();
            using (ByteArraySlice byteArraySlice = photonEvent.CustomData as ByteArraySlice)
            {
                //The data received
                ArraySegment<byte> data = new ArraySegment<byte>(byteArraySlice.Buffer, byteArraySlice.Offset, byteArraySlice.Count);

                if (photonEvent.Sender == client.CurrentRoom.MasterClientId)
                {
                    //Sent by server
                    //The channel is "encoded" on the ArraySegment
                    byte channelId = data[data.Count - 1];
                    data = new ArraySegment<byte>(byteArraySlice.Buffer, 0, data.Count - 1);
                    Channel channel = channelId == 0 ? Channel.Reliable : Channel.Unreliable;
                    ClientReceivedDataArgs args = new ClientReceivedDataArgs(data, channel, Index);
                    HandleClientReceivedDataArgs(args);
                }
                else
                {
                    //Sent by client
                    //The channel is "encoded" on the ArraySegment
                    byte channelId = data[data.Count - 1];
                    data = new ArraySegment<byte>(byteArraySlice.Buffer, 0, data.Count - 1);
                    Channel channel = channelId == 0 ? Channel.Reliable : Channel.Unreliable;
                    ServerReceivedDataArgs args = new ServerReceivedDataArgs(data, channel, photonEvent.Sender - 1, Index);
                    HandleServerReceivedDataArgs(args);
                }
            }
        }

        //A fake method for receiving data as host
        void SendHost(ArraySegment<byte> data)
        {
            byte channelId = data[data.Count - 1];
            data = new ArraySegment<byte>(data.Array, 0, data.Count - 1);
            Channel channel = channelId == 0 ? Channel.Reliable : Channel.Unreliable;
            ServerReceivedDataArgs args = new ServerReceivedDataArgs(data, channel, 0, Index);
            HandleServerReceivedDataArgs(args);
        }

        #endregion

        #region Connecting

        //It will just create or join a room
        public override bool StartConnection(bool server)
        {
            if (server)
            {
                Room room = new Room()
                {
                    //A random name
                    name = "Room " + UnityEngine.Random.Range(0, 1000).ToString(),
                    //Use the default settings
                    isPublic = isVisible,
                    open = isOpen,
                    maxPlayers = maxPlayers
                };
                CreateRoom(room);
            }
            else
            {
                JoinRandomRoom();
            }
            return true;
        }

        //Leave the room
        public override bool StopConnection(bool server)
        {
            LeaveRoom();

            return true;
        }

        //Send it as a event, IDK if its good to make it like that
        public override bool StopConnection(int connectionId, bool immediately)
        {
            eventOptions.TargetActors[0] = connectionId + 1;
            return client.OpRaiseEvent(1, null, eventOptions, SendOptions.SendReliable);
        }

        public override void Shutdown()
        {
            Disconnect();
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            isConnectedToMaster = false;
            //Some simple logging
            Debug.Log("Fishy Realtime disconnected " + cause);
        }

        public void OnCreatedRoom()
        {

        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError("Cant start server for Fishy Realtime, " + message + " " + returnCode);
        }

        public void OnJoinedRoom()
        {
            //If started server
            if (client.LocalPlayer.IsMasterClient)
            {
                ServerConnectionStateArgs serverArgs = new ServerConnectionStateArgs(GetConnectionState(true), Index);
                HandleServerConnectionState(serverArgs);
                RemoteConnectionStateArgs remoteArgs = new RemoteConnectionStateArgs(RemoteConnectionState.Started, 0, Index);
                HandleRemoteConnectionState(remoteArgs);
            }
            //Always start client
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
            HandleClientConnectionState(clientArgs);
            //If there is a name, set it
            if (cahedPlayerName != "") client.LocalPlayer.NickName = cahedPlayerName;

        }

        //Logging
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.Log("Fishy Realtime failed to connect, " + message + " " + returnCode);
        }

        public void OnLeftRoom()
        {
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(LocalConnectionState.Stopped, Index);
            HandleClientConnectionState(clientArgs);
            if (NetworkManager.IsServer)
            {
                RemoteConnectionStateArgs argsRemote = new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, 0, Index);
                HandleRemoteConnectionState(argsRemote);
                ServerConnectionStateArgs args = new ServerConnectionStateArgs(LocalConnectionState.Stopped, Index);
                HandleServerConnectionState(args);
            }
        }

        //A remote connection has to be 
        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (NetworkManager.IsServer)
            {
                RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(GetConnectionState(newPlayer.ActorNumber), newPlayer.ActorNumber - 1, Index);
                HandleRemoteConnectionState(state);
            }
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (otherPlayer.ActorNumber == 1)
            {
                LeaveRoom();
                return;
            }
            if (NetworkManager.IsServer)
            {
                if (otherPlayer.ActorNumber == 1 && otherPlayer != client.LocalPlayer) return;
                //Stop the remote connection
                RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(GetConnectionState(otherPlayer.ActorNumber), otherPlayer.ActorNumber - 1, Index);
                HandleRemoteConnectionState(state);
            }
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(LocalConnectionState.Stopped, Index);
            HandleClientConnectionState(clientArgs);
        }
        #endregion

        //Photons default MTU
        public override int GetMTU(byte channel)
        {
            return 1200;
        }
        #region Photon Events
        public void OnConnected()
        {
        }





        public void OnRegionListReceived(RegionHandler regionHandler)
        {

        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {

        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {

        }

        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {

        }

        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {

        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {

        }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
        }

        public void OnJoinedLobby()
        {

        }

        public void OnLeftLobby()
        {

        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {

        }

        #endregion

        #region Matchmaking

        /// <summary>
        /// Joins a random room
        /// </summary>
        /// <param name="createOnFail">True to create a room if failed to join</param>
        public void JoinRandomRoom(bool createOnFail = false)
        {
            if (createOnFail)
            {
                EnterRoomParams createParams = new EnterRoomParams()
                {
                    RoomName = "Room " + UnityEngine.Random.Range(0, 1000).ToString(),
                    RoomOptions = new RoomOptions()
                    {
                        IsOpen = isOpen,
                        IsVisible = isVisible,
                        MaxPlayers = maxPlayers
                    },
                };
                client.OpJoinRandomOrCreateRoom(null, createParams);
            }
            else client.OpJoinRandomRoom();
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
            HandleClientConnectionState(clientArgs);
        }

        /// <summary>
        /// Joins a random room with filters
        /// </summary>
        /// <param name="filter">The filter to use in order to search for rooms</param>
        /// <param name="createOnFail">True to create a room if failed to join</param>
        public void JoinRandomRoom(RoomFilter filter, bool createOnFail = false)
        {
            ExitGames.Client.Photon.Hashtable customProperties = new ExitGames.Client.Photon.Hashtable();

            if (filter.gameMode != null) customProperties.Add("GameMode", filter.gameMode);
            if (filter.mapName != null) customProperties.Add("Map", filter.gameMode);

            OpJoinRandomRoomParams joinParams = new OpJoinRandomRoomParams()
            {
                ExpectedCustomRoomProperties = customProperties
            };
            if (createOnFail)
            {
                EnterRoomParams enterParams = new EnterRoomParams()
                {
                    RoomName = "Room " + UnityEngine.Random.Range(0, 1000).ToString(),
                    RoomOptions = new RoomOptions()
                    {
                        CustomRoomProperties = customProperties
                    },
                };
                client.OpJoinRandomOrCreateRoom(joinParams, enterParams);
            }
            else
            {
                client.OpJoinRandomRoom(joinParams);
            }
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
            HandleClientConnectionState(clientArgs);
        }

        /// <summary>
        /// Creates a room
        /// </summary>
        /// <param name="info">The data used to create the room</param>
        public void CreateRoom(Room info)
        {
            string roomName = info.name == null ? "Room " + UnityEngine.Random.Range(0, 1000) : info.name;
            EnterRoomParams roomParams = new EnterRoomParams()
            {
                RoomName = roomName,
                RoomOptions = new RoomOptions()
                {
                    MaxPlayers = info.maxPlayers,
                    IsVisible = info.isPublic,
                    IsOpen = info.open
                }
            };
            client.OpCreateRoom(roomParams);
        }

        /// <summary>
        /// Creates a room with custom data
        /// </summary>
        /// <param name="info"></param>
        /// <param name="customData"></param>
        public void CreateRoom(Room info, RoomFilter customData)
        {
            string roomName = info.name == null ? "Room " + UnityEngine.Random.Range(0, 1000) : info.name;
            EnterRoomParams roomParams = new EnterRoomParams()
            {
                RoomName = roomName,
                RoomOptions = new RoomOptions()
                {
                    MaxPlayers = info.maxPlayers,
                    IsVisible = info.isPublic,
                    IsOpen = info.open,
                    CustomRoomProperties = new ExitGames.Client.Photon.Hashtable()
                    {
                        {
                            "Map", customData.mapName
                        },
                        {
                            "GameMode", customData.gameMode
                        }
                    },
                    CustomRoomPropertiesForLobby = new string[] { "Map", "GameMode" }
                }
            };

            client.OpCreateRoom(roomParams);

            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
            HandleClientConnectionState(clientArgs);
            ServerConnectionStateArgs serverArgs = new ServerConnectionStateArgs(GetConnectionState(true), Index);
            HandleServerConnectionState(serverArgs);
        }

        /// <summary>
        /// Joins a room with a specified name
        /// </summary>
        /// <param name="name">The name of the room</param>
        public void JoinRoom(string name)
        {
            EnterRoomParams roomParams = new EnterRoomParams()
            {
                RoomName = name
            };

            client.OpJoinRoom(roomParams);
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
            HandleClientConnectionState(clientArgs);
        }

        /// <summary>
        /// Joins a room with the specified data
        /// </summary>
        /// <param name="room">The room to connect</param>
        public void JoinRoom(Room room)
        {
            EnterRoomParams roomParams = new EnterRoomParams()
            {
                RoomName = room.name,
                RoomOptions = new RoomOptions()
                {
                    MaxPlayers = room.maxPlayers,
                    IsVisible = room.isPublic,
                    IsOpen = room.open
                },
            };
            client.OpJoinRoom(roomParams);
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
            HandleClientConnectionState(clientArgs);
        }

        /// <summary>
        /// Get the current room
        /// </summary>
        /// <returns>The current room</returns>
        public Room GetCurrentRoom()
        {
            Photon.Realtime.Room currRoom = client.CurrentRoom;
            Room room = new Room()
            {
                name = currRoom.Name,
                isPublic = currRoom.IsVisible,
                open = currRoom.IsOpen,
                maxPlayers = currRoom.MaxPlayers,
                playerCount = currRoom.PlayerCount

            };
            return room;
        }

        /// <summary>
        /// Get the current room filter
        /// </summary>
        /// <returns>The filter of the room, can be used for getting the map and gamemode</returns>
        public RoomFilter GetRoomFilter()
        {
            ExitGames.Client.Photon.Hashtable filterData = client.CurrentRoom.CustomProperties;
            RoomFilter filter = new RoomFilter();
            if (filterData.TryGetValue("Map", out object map)) filter.mapName = map.ToString();
            if (filterData.TryGetValue("GameMode", out object gameMode)) filter.gameMode = gameMode.ToString();
            return filter;
        }

        /// <summary>
        /// Gets all the rooms
        /// </summary>
        /// <returns>The room list</returns>
        public Room[] GetRoomList()
        {
            Room[] rooms = new Room[cachedRooms.Count];
            int i = 0;
            foreach (KeyValuePair<string, RoomInfo> entry in cachedRooms)
            {
                rooms[i] = new Room()
                {
                    name = entry.Value.Name,
                    isPublic = entry.Value.IsVisible,
                    open = entry.Value.IsOpen,
                    maxPlayers = entry.Value.MaxPlayers,
                    playerCount = entry.Value.PlayerCount
                };
                i++;
            }
            return rooms;
        }

        /// <summary>
        /// Gets all the rooms with a specified filter
        /// </summary>
        /// <param name="filter">The filter</param>
        /// <returns>The room list</returns>
        public Room[] GetRoomList(RoomFilter filter)
        {
            List<Room> rooms = new List<Room>();
            foreach (KeyValuePair<string, RoomInfo> entry in cachedRooms)
            {
                if (entry.Value.CustomProperties.TryGetValue("GameMode", out object gameMode)) if (filter.gameMode != null && filter.gameMode != gameMode.ToString()) continue;

                if (entry.Value.CustomProperties.TryGetValue("Map", out object mapName)) if (filter.mapName != null && filter.mapName != mapName.ToString()) continue;

                Room room = new Room()
                {
                    name = entry.Value.Name,
                    isPublic = entry.Value.IsVisible,
                    open = entry.Value.IsOpen,
                    maxPlayers = entry.Value.MaxPlayers,
                    playerCount = entry.Value.PlayerCount
                };
                rooms.Add(room);
            }
            return rooms.ToArray();
        }

        /// <summary>
        /// Leaves the room
        /// </summary>
        public void LeaveRoom()
        {
            client.OpLeaveRoom(false);
        }

        /// <summary>
        /// Disconnects from the master server
        /// </summary>
        public void Disconnect()
        {
            if(client.InRoom)LeaveRoom();
            client.Disconnect();
            client.RemoveCallbackTarget(this);
        }

        public void ConnectToRegion(Region region)
        {
            if (client.IsConnected) Disconnect();
            client.AddCallbackTarget(this);

            string regionStr;

            switch (region)
            {
                case Region.Asia:
                    regionStr = "asia";
                    break;
                case Region.Australia:
                    regionStr = "au";
                    break;
                case Region.CanadaEast:
                    regionStr = "cae";
                    break;
                case Region.Europe:
                    regionStr = "eu";
                    break;
                case Region.India:
                    regionStr = "in";
                    break;
                case Region.Japan:
                    regionStr = "jp";
                    break;
                case Region.Russia:
                    regionStr = "ru";
                    break;
                case Region.RussiaEast:
                    regionStr = "rue";
                    break;
                case Region.SouthAfrica:
                    regionStr = "za";
                    break;
                case Region.SouthAmerica:
                    regionStr = "sa";
                    break;
                case Region.SouthKorea:
                    regionStr = "kr";
                    break;
                case Region.Turkey:
                    regionStr = "tr";
                    break;
                case Region.USAEast:
                    regionStr = "us";
                    break;
                case Region.USAWest:
                    regionStr = "usw";
                    break;
                default:
                    //Just to prevent errors
                    regionStr = "eu";
                    break;
            }

            AppSettings settings = new AppSettings()
            {
                AppIdRealtime = photonAppId,
                AppVersion = appVersion,
                Protocol = connectionProtocol,
                FixedRegion = regionStr
            };

            client.LoadBalancingPeer.UseByteArraySlicePoolForEvents = true;

            client.ConnectUsingSettings(settings);
        }

        public void OnConnectedToMaster()
        {
            isConnectedToMaster = true;
            //No args are needed
            if(ConnectedToMaster != null) ConnectedToMaster.Invoke(this, EventArgs.Empty);
            client.OpJoinLobby(TypedLobby.Default);
        }

        public event EventHandler ConnectedToMaster;

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            for (int i = 0; i < roomList.Count; i++)
            {
                //If the room was deleted, remove it from list
                if (roomList[i].RemovedFromList)
                {
                    cachedRooms.Remove(roomList[i].Name);
                }
                //If not, update it or add it
                else
                {
                    cachedRooms[roomList[i].Name] = roomList[i];
                }
            }
        }

        #endregion

        #region Username

        /// <summary>
        /// The name of the player
        /// </summary>
        public string playerName
        {
            get
            {
                if (client.InRoom) return client.LocalPlayer.NickName;
                else return cahedPlayerName;
            }
            set
            {
                if (client.InRoom) client.LocalPlayer.NickName = value;
                else cahedPlayerName = value;
            }
        }

        private string cahedPlayerName = "";

        /// <summary>
        /// Gets the player name
        /// </summary>
        /// <param name="id">The id of the player</param>
        /// <returns>The player's name</returns>
        public string GetPlayerUsername(int id)
        {
            return client.CurrentRoom.Players[id + 1].NickName;
        }

        #endregion

        public static bool isConnectedToMaster = false;
    }
}