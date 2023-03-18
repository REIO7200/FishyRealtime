FishyRealtime
A photon realtime transport for Fish-Networking

Dependencies:
-Fish-Netowking: https://github.com/FirstGearGames/FishNet

-Photon Realtime: https://www.photonengine.com/en-US/Realtime

Setup
-Create a Photon Account: https://id.photonengine.com/en-US/Account/SignUp
-After creating the account, download the Unity SDK from here, and copy the folder called "Photon" into your unity project.
-Go to your Photon Dashboard
-Click on "Create a new app"
-Leave the Photon Type at realtime, and after filling the name and description, click on "Create"
-Now download this transport, and import it into Unity
-Add a FishyRealtime component and a TransportManager to your NetworkManager
-Set the transport field of the TransportManager to the FishyRealtime you just added
-Go back to the Photon Dashboard and on your project copy the App ID to the FishyRealtime's App Id field
-In the Version put whatever you like. Keep in mind that clients with different versions cant connect to eachother
-You can leave the socket type to UDP, but if you are making a WebGL game, change it to Web Socket
-You must set your NetworkManagers persistence to DestroyNewest. I couldnt find a way to make it to work with the other two
-Thats it!

Matchmaking:
-FishyRealtime has its own matchmaking API, documentation coming soon
