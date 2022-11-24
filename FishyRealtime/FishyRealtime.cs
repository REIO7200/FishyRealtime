using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Transporting;
using System;
using Photon.Realtime;
using FishNet;
using ExitGames.Client.Photon;
using FishNet.Managing.Transporting;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Connection;
using FishyRealtime.Migration;

namespace FishyRealtime
{
    public class FishyRealtime : Transport, IConnectionCallbacks, IMatchmakingCallbacks, IOnEventCallback, IInRoomCallbacks
    {
        private LoadBalancingClient client = new LoadBalancingClient();

        //Do I have to explain what is?
        private RaiseEventOptions eventOptions = new RaiseEventOptions()
        {
            TargetActors = new int[1]
        };

        [Tooltip("The app id of the photon realtime product")]
        [SerializeField] private string photonAppId;

        [Tooltip("The version of the product(Game), only clients with the same version can connect to each other")]
        [SerializeField] private string appVersion = "0.0.1";

        [Tooltip("The name of the room to create or join")]
        public string roomName = "Room";

        [SerializeField] private byte maxPlayers = 10;

        [Tooltip("What is goinng to be used to connect")]
        public ConnectionProtocol connectionProtocol;

        [Tooltip("True to migrate the host when it leaves")]
        public bool migrateHost = true;


        bool isServer = false;

        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;

        public Dictionary<int, int> PhotonIdToFishNet = new Dictionary<int, int>();
        public Dictionary<int, int> FishNetIdToPhoton = new Dictionary<int, int>();

        //?
        public override string GetConnectionAddress(int connectionId)
        {
            return "";
        }

        //Set the room name
        public override void SetClientAddress(string address)
        {
            roomName = address;
        }

        private void Start()
        {
            
        }

        private void FixedUpdate()
        {
            
        }

        #region Connection state
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
                case ClientState.ConnectingToNameServer:
                    return LocalConnectionState.Starting;
                case ClientState.Joined:
                    return LocalConnectionState.Started;
                case ClientState.DisconnectingFromMasterServer:
                    return LocalConnectionState.Stopping;
            }
            return LocalConnectionState.Stopped;
        }

        public override RemoteConnectionState GetConnectionState(int connectionId)
        {
            try
            {
                if (client.CurrentRoom.Players[connectionId] == null) return RemoteConnectionState.Stopped;
            }
            catch
            {
                return RemoteConnectionState.Stopped;
            }
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

            if(NetworkManager.IsHost)
            {
                SendHost(segment);
            }

            client.OpRaiseEvent(0, segment, RaiseEventOptions.Default, options);
        }

        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
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

            client.OpRaiseEvent(1, segment, eventOptions, options);
        }

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
            //client.Service();
        }

        public override void IterateOutgoing(bool server)
        {
            client.Service();
        }

        //Called when a photon event is received
        public void OnEvent(EventData photonEvent)
        {
            //If it is a internal event, nothing to do
            if (photonEvent.Code >= 200) return;

            using (ByteArraySlice byteArraySlice = photonEvent.CustomData as ByteArraySlice)
            {
                ArraySegment<byte> data = new ArraySegment<byte>(byteArraySlice.Buffer, byteArraySlice.Offset, byteArraySlice.Count);
                //Migration data
                if(photonEvent.Code == 3)
                {
                    Debug.Log("fe");
                    FishNet.Serializing.Reader reader = new FishNet.Serializing.Reader(data, NetworkManager);
                    GameObject[] objects = reader.Read<GameObject[]>();
                    foreach (GameObject obj in objects)
                    {
                        Instantiate(obj);
                    }
                }
                if (photonEvent.Code == 1)
                {
                    //Sent by server
                    byte channelId = data[data.Count - 1];
                    data = new ArraySegment<byte>(byteArraySlice.Buffer, 0, data.Count - 1);
                    Channel channel = channelId == 0 ? Channel.Reliable : Channel.Unreliable;
                    ClientReceivedDataArgs args = new ClientReceivedDataArgs(data, channel, Index);
                    HandleClientReceivedDataArgs(args);
                }
                else
                {
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
        public override bool StartConnection(bool server)
        {
            //Setup the Realtime callbacks
            client.AddCallbackTarget(this);

            AppSettings settings = new AppSettings()
            {
                AppIdRealtime = photonAppId,
                AppVersion = appVersion,
                Protocol = connectionProtocol,
                FixedRegion = "eu"
            };

            client.LoadBalancingPeer.UseByteArraySlicePoolForEvents = true;
            if (server)
            {
                isServer = true;
            }
            else
            {
                isServer = false;
            }
            bool connect = client.ConnectUsingSettings(settings);
            if (server)
            {
                ServerConnectionStateArgs args = new ServerConnectionStateArgs(GetConnectionState(true), Index);
                HandleServerConnectionState(args);
            }
            else
            {
                ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
                HandleClientConnectionState(clientArgs);
                if (NetworkManager.IsServer)
                {
                    RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(RemoteConnectionState.Started, 0, Index);
                    HandleRemoteConnectionState(state);
                }
            }
            return connect;
        }

        public override bool StopConnection(bool server)
        {
            client.Disconnect();

            client.RemoveCallbackTarget(this);
            if (!server && NetworkManager.IsServer)
            {
                ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(LocalConnectionState.Stopped, Index);
                HandleClientConnectionState(clientArgs);
                RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, 0, Index);
                HandleRemoteConnectionState(state);
                return true;
            }
            
            if (server)
            {
                ServerConnectionStateArgs args = new ServerConnectionStateArgs(GetConnectionState(true), Index);
                HandleServerConnectionState(args);
            }
            else
            {
                ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
                HandleClientConnectionState(clientArgs);
            }

            
            return true;
        }

        //IDK what to do with this      
        public override bool StopConnection(int connectionId, bool immediately)
        {
            return false;
        }

        public override void Shutdown()
        {
            client.Disconnect();
            client.RemoveCallbackTarget(this);
        }

        public void OnConnectedToMaster()
        {
            RoomOptions options = new RoomOptions()
            {
                MaxPlayers = maxPlayers,
                IsVisible = true,
            };
            EnterRoomParams roomParams = new EnterRoomParams()
            {
                RoomName = roomName,
                RoomOptions = options,
            };
            if (isServer) client.OpCreateRoom(roomParams);
            else client.OpJoinRoom(roomParams);
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log("Fishy Realtime disconnected" + cause);
        }

        public void OnCreatedRoom()
        {
            ServerConnectionStateArgs args = new ServerConnectionStateArgs(GetConnectionState(true), Index);
            HandleServerConnectionState(args);
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError("Cant start server for Fishy Realtime, " + message + " " + returnCode);
        }

        public void OnJoinedRoom()
        {
            FishNetIdToPhoton.Add(client.LocalPlayer.ActorNumber - 1, client.LocalPlayer.ActorNumber);
            if (!base.NetworkManager.IsServer)
            {
                ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(GetConnectionState(false), Index);
                HandleClientConnectionState(clientArgs);
            }

        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.Log("Fishy Realtime failed to connect, " + message + " " + returnCode);
        }

        public void OnLeftRoom()
        {
            Debug.Log("Fishy Realtime left room");
        }


        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            FishNetIdToPhoton.Add(newPlayer.ActorNumber - 1, newPlayer.ActorNumber);
            PhotonIdToFishNet.Add(newPlayer.ActorNumber, newPlayer.ActorNumber - 1);
            if (isServer)
            {
                RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(GetConnectionState(newPlayer.ActorNumber), newPlayer.ActorNumber - 1, Index);
                HandleRemoteConnectionState(state);
            }
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (NetworkManager.IsServer)
            {
                if (otherPlayer.ActorNumber == 1 && otherPlayer != client.LocalPlayer) return;
                RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(GetConnectionState(otherPlayer.ActorNumber), otherPlayer.ActorNumber - 1, Index);
                HandleRemoteConnectionState(state);
            }
        }
        #endregion

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



        public void OnJoinRandomFailed(short returnCode, string message)
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
            if (!migrateHost) return;
            if (newMasterClient == client.LocalPlayer)
            {
                ServerConnectionStateArgs args = new ServerConnectionStateArgs(LocalConnectionState.Started, Index);
                HandleServerConnectionState(args);
                NetworkObject[] networkObjects = FindObjectsOfType<NetworkObject>();
                for (int i = 0; i < networkObjects.Length; i++)
                {
                    HostMigration migrator = networkObjects[i].gameObject.AddComponent<HostMigration>();
                    migrator.playerID = FishNetIdToPhoton[networkObjects[i].OwnerId];
                    networkObjects[i].RemoveOwnership();
                }
                FishNetIdToPhoton.Clear();

                foreach (KeyValuePair<int, Player> entry in client.CurrentRoom.Players)
                {
                    RemoteConnectionStateArgs remoteArgs = new RemoteConnectionStateArgs(RemoteConnectionState.Started, entry.Value.ActorNumber - 1, Index);
                    HandleRemoteConnectionState(remoteArgs);
                    FishNetIdToPhoton.Add(entry.Value.ActorNumber - 1, entry.Value.ActorNumber);
                    PhotonIdToFishNet.Add(entry.Value.ActorNumber, entry.Value.ActorNumber - 1);
                }

                networkObjects = FindObjectsOfType<NetworkObject>();
                for (int i = 0; i < networkObjects.Length; i++)
                {
                    //If this got spawned by the PlayerSpawner, remove it
                    if (!networkObjects[i].gameObject.TryGetComponent(out HostMigration _)) networkObjects[i].Despawn(networkObjects[i].gameObject);
                }

                HostMigration[] migrators = FindObjectsOfType<HostMigration>();
                foreach (KeyValuePair<int, NetworkConnection> entry in NetworkManager.ServerManager.Clients)
                {
                    for (int i = 0; i < migrators.Length; i++)
                    {
                        if (!PhotonIdToFishNet.TryGetValue(migrators[i].playerID, out int index)) continue;
                        if(index == entry.Value.ClientId)
                        {
                            Debug.Log(entry.Value.ClientId);
                            
                            migrators[i].GetComponent<NetworkObject>().GiveOwnership(entry.Value);
                            Debug.Log("dpfjkñs");
                        }
                    }
                }

            }
        }

        #endregion
    }
}