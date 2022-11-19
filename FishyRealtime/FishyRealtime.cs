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
    [SerializeField] private string appVersion;

    [Tooltip("The name of the room to create or join")]
    public string roomName;

    [SerializeField] private byte maxPlayers;

    [Tooltip("What is goinng to be used to connect")]
    public ConnectionProtocol socketType;

    bool isServer = false;


    public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
    public override event Action<ServerReceivedDataArgs> OnServerReceivedData;

    //?
    public override string GetConnectionAddress(int connectionId)
    {
        return client.CurrentRoom.Players[connectionId + 1].UserId;
    }

    //Set the room name
    public override void SetClientAddress(string address)
    {
        roomName = address;
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
            case ClientState.ConnectedToGameServer:
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

        client.OpRaiseEvent(0, segment, eventOptions, options);
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
        client.Service();
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

            if (photonEvent.Sender == 1)
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
            Protocol = socketType,
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
                RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(GetConnectionState(1), 0, Index);
                HandleRemoteConnectionState(state);
            }
        }
        return connect;
    }

    
    public override bool StopConnection(bool server)
    {
        if(!server && NetworkManager.IsServer)
        {
            ClientConnectionStateArgs clientArgs = new ClientConnectionStateArgs(LocalConnectionState.Stopped, Index);
            HandleClientConnectionState(clientArgs);
            return false;
        }
        client.Disconnect();

        client.RemoveCallbackTarget(this);
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
        if (isServer)
        {
            RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(GetConnectionState(newPlayer.ActorNumber), newPlayer.ActorNumber - 1, Index);
            HandleRemoteConnectionState(state);
        }
    }

    public void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (otherPlayer.ActorNumber == 1)
        {
            StopConnection(isServer);
        }
        if (isServer)
        {
            RemoteConnectionStateArgs state = new RemoteConnectionStateArgs(GetConnectionState(otherPlayer.ActorNumber), otherPlayer.ActorNumber - 1, Index);
            HandleRemoteConnectionState(state);
        }
    }
    #endregion

    public override int GetMTU(byte channel)
    {
        return 1500;
    }

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

    }
}
