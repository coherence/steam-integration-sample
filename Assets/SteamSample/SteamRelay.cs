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
            Debug.Log($"SteamRelay.OnConnecting #{steamConnection.Id}");

            steamConnection.ConnectionName = $"#{info.Identity.SteamId}";
            var relayConnection = new SteamRelayConnection(steamConnection);
            RelayManager.OpenRelayConnection(relayConnection);

            connectionMap.Add(steamConnection, relayConnection);
        }

        public void OnConnected(Connection steamConnection, ConnectionInfo info)
        {
            Debug.Log($"SteamRelay.OnConnected #{steamConnection.Id}");
        }

        public void OnDisconnected(Connection steamConnection, ConnectionInfo info)
        {
            Debug.Log($"SteamRelay.OnDisconnected #{steamConnection.Id}: {info.State} EndReason: {info.EndReason} ({(int)info.EndReason}) Address: {info.Address} Identity: {info.Identity}");

            if (!connectionMap.TryGetValue(steamConnection, out var relayConnection))
            {
                Debug.LogError($"Steam client #{steamConnection.Id} not found in connection map.");
                return;
            }

            RelayManager.CloseAndRemoveRelayConnection(relayConnection);

            connectionMap.Remove(steamConnection);
        }

        public void OnMessage(Connection steamConnection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            // Copy packet data into managed byte array.
            // The reason for skipping the header is, SteamTransport pads the message with UDP transport header.
            // This is done to match the UDP transport MTU, otherwise SteamTransport could send a 1280 bytes packet
            // which would require 1284 bytes in UDP transport, thus would be unsendable to RS on the host.
            var headerlessSize = size - SteamTransport.HeaderSizeBytes;
            var packet = new byte[headerlessSize];
            Marshal.Copy(data + SteamTransport.HeaderSizeBytes, packet, 0, headerlessSize);

            if (!connectionMap.TryGetValue(steamConnection, out var relayConnection))
            {
                Debug.Log($"SteamRelay.OnMessage: Steam client #{steamConnection.Id} not found in connection map.");
                return;
            }

            // Push message to the relay connection
            relayConnection.EnqueueMessageFromSteam(new ArraySegment<byte>(packet));
        }
    }
}