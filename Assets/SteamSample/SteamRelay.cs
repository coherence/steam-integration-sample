using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Coherence.Toolkit.Relay;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SteamSample
{
    public class SteamRelay : IRelay, ISocketManager
    {
        private readonly Dictionary<Connection, SteamRelayConnection> connectionMap = new Dictionary<Connection, SteamRelayConnection>();
        private SocketManager steamSocketManager;

        ////////////////////////////////////////////////
        // ICoherenceRelayManager
        ////////////////////////////////////////////////

        public CoherenceRelayManager RelayManager { get; set; }

        public void Open()
        {
            var serverInit = new SteamServerInit(Application.productName, Application.productName);

            if (!SteamServer.IsValid)
            {
                SteamServer.Init(SteamClient.AppId, serverInit, false);
                Debug.Log($"{nameof(SteamRelay)} initialized");
            }

            steamSocketManager = SteamNetworkingSockets.CreateRelaySocket(0, this);
        }

        public void Update()
        {
            if (!SteamServer.IsValid)
            {
                throw new Exception("SteamServer is not valid");
            }

            steamSocketManager.Receive();

            SteamServer.RunCallbacks();
        }

        public void Close()
        {
            if (SteamServer.IsValid)
            {
                SteamServer.Shutdown();
            }

            if (steamSocketManager != null)
            {
                steamSocketManager.Close();
                steamSocketManager = null;
            }
        }

        ////////////////////////////////////////////////
        // ISocketManager
        ////////////////////////////////////////////////

        public void OnConnecting(Connection steamConnection, ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamRelay)} OnConnecting: {info.State} ID: {steamConnection.Id}");

            steamConnection.ConnectionName = $"#{info.Identity.SteamId}";
            var relayConnection = new SteamRelayConnection(steamConnection);
            RelayManager.OpenRelayConnection(relayConnection);

            connectionMap.Add(steamConnection, relayConnection);
        }

        public void OnConnected(Connection steamConnection, ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamRelay)} OnConnected: {info.State}");
        }

        public void OnDisconnected(Connection steamConnection, ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamRelay)} OnDisconnected: {info.State}");

            if (!connectionMap.TryGetValue(steamConnection, out var relayConnection))
            {
                Debug.LogError($"{nameof(SteamRelay)} Failed to find client for connection with Id: {steamConnection.Id}");
                return;
            }

            RelayManager.CloseAndRemoveRelayConnection(relayConnection);

            connectionMap.Remove(steamConnection);
        }

        public void OnMessage(Connection steamConnection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            // Copy packet data into managed byte array
            var packet = new byte[size];
            Marshal.Copy(data, packet, 0 , size);

            if (!connectionMap.TryGetValue(steamConnection, out var relayConnection))
            {
                Debug.LogError($"{nameof(SteamRelay)} Failed to find client for connection with Id: {steamConnection.Id}");
                return;
            }

            // Push message to the relay connection
            relayConnection.EnqueueMessageFromSteam(new ArraySegment<byte>(packet));
        }
    }
}