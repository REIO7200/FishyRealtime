# Matchmaking API

## Important
If you dont want to keep a reference to the FishyRealtime component, you can use the singleton (FishyRealtime.Instance) to call all the methods
In order to use this, you **MUST** be connected to the master server. You can subscribe to the ConnectedToMaster event to know when you connect

## Functions and Fields

### void JoinRandomRoom(bool createOnFail = false)
It will join a random room on the master server. Set createOnFail to true if you want to create a room if no room was found

### void JoinRandomRoom(RoomFilter filter, bool createOnFail = false)
It will join a random room on the master server with the specified filter. Set createOnFail to true if you want to create a room if no room was found

**Note**: If the RoomFilter's map or gamemode field is empty, that field will be ignored

### void CreateRoom(Room info)
It creates a room with the specified data

### void CreateRoom(RoomInfo info, RoomFilter customData)
It creates a room with the specified data. The RoomFilter will be used to list rooms, join random..

### void JoinRoom(string name)
It will try to join a room with that name

### void JoinRoom(Room room)
It will try to join a room with that parameters

### public Room GetCurrenRoom()
It will return the current room's data. The client **MUST** be on a room

### public RoomFilter GetRoomFilter()
It will return the current room's filter. The client **MUST** be on a room

### public Room[] GetRoomList()
It will return all rooms on the master server

### public Room[] GetRoomList(RoomFilter filter)
It will return all rooms on the master server that have the RoomFilter's properties

**Note**: If the RoomFilter's map or gamemode field is empty, that field will be ignored

### public void LeaveRoom()
It will leave the current room, but it wont disconnect from the master server

### public void Disconnect()
It will leave the room **and** disconnect from the master server

### public void ConnectToRegion(Region region)
It disconnects and connects to a new region

### public string playerName
The name of the player, keep in mind that this transport doesnt have a account system, this is just a virtual name

### public string GetPlayerName(int id)
Returns the username of the specified player

### public static bool isConnectedToMaster
Is the player connected to the master server?

## Structs and Enums

### Room
It represents a room on the photon server, and has all the data needed to create or join one

#### string name
This is the name of the room, if empty a random name will be generated, the name will be something like Room (Random number between 0 and 999)

#### bool isPublic
Set it to false if you want the room to be listed with GetRoomList() and can be joined with JoinRandomRoom(), the room can stil be joined with JoinRoom()

#### bool open
Set it to false to prevent clients from joining

#### byte maxPlayers
Max ammount of players that can join that room

#### int playerCount
The number of players on that room

### RoomFilter
Custom data for rooms, it has two strings: mapName and gamemode, but you can set it to anything you want

### Region
A enum for all the regions that can be joined, in order to play together, two players **MUST** be on the same region
