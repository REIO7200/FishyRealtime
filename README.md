# FishyRealtime
A photon realtime transport for Fish-Networking

If you have any issues, ping me (REIO 7200#2377) on the [FirstGearGames discord](https://discord.gg/Ta9HgDh4Hj)

## Dependencies

Fish-Networking: https://github.com/FirstGearGames/FishNet

Photon Realtime: https://www.photonengine.com/en-US/Realtime

## Setup

1. Create a Photon Account: https://id.photonengine.com/en-US/Account/SignUp
2. After creating the account, download the Unity SDK from [here](https://www.photonengine.com/en-US/sdks#realtime-unity-sdkrealtimeunity), and copy the folder called "Photon" into your unity project.
4. Go to your [Photon Dashboard](https://dashboard.photonengine.com/en-US/)
5. Click on "Create a new app"
6. Leave the Photon Type at realtime, and after filling the name and description, click on "Create"
7. Now download this transport, and import it into Unity
8. Add a FishyRealtime component and a TransportManager to your NetworkManager
9. Set the transport field of the TransportManager to the FishyRealtime you just added
10. Go back to the Photon Dashboard and on your project copy the App ID to the FishyRealtime's App Id field
11. In the Version put whatever you like. Keep in mind that clients with different versions cant connect to eachother
12. You can leave the socket type to UDP, but if you are making a WebGL game, change it to Web Socket
13. You must set your NetworkManagers persistence to DestroyNewest. I couldnt find a way to make it to work with the other two
14. Thats it! 

## Matchmaking

FishyRealtime has its own matchmaking, documentated [here](https://github.com/REIO7200/FishyRealtime/blob/main/FishyRealtime/MatchmakingAPI.md)

## About the pricing

FishyRealtime is completely free to use, but photon realime isnt. More info [here](https://www.photonengine.com/en/realtime/pricing#)
