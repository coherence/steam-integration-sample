# Steam Integration Sample

A sample integration for the Steam P2P Relay and coherence networking for player hosted servers.

## How does it work

The Steam Integration is using a combination of the Steam relay servers and an implementation of `ICoherenceRelay` to enables Steam users to connect and play with eachother, while avoiding NAT issues.
The `ICoherenceRelay` implementation allows for users to connect to the hosting client through Steam, and have the client forward their data packets to the user hosted replication server.

## Components

`SteamManager` will initialize the SteamSDK and manage the joining and hosting of self-hosted servers.

`SteamLobbyUI` is an easy sample to provide hosting and joining accessibility, that will provide a UI in the top left of the screen.

## How to use in your own project

1. Copy the "SteamSample" folder into your own project.
1. In the scene containing your `CoherenceBridge` Gameobject/Component, Create a new Gameobject with the `SteamManager` Component.
1. (Optional) Add the `SteamLobbyUI` to the same Gameobject.
1. Use the Inspector with the `SteamManager` to set the `Steam App Id` to the Id of your project.

You can now use the integration through either the SteamLobbyUI, or the `SteamManager` right-click menu in the Inspector.

![Scene layout with SteamManager](/.github/images/scene-setup.png)

![SteamManager Context menu](/.github/images/steammanager-context-menu.png)
