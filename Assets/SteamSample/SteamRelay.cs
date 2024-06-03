using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Coherence.Toolkit.Relay;
using Steamworks;
using Steamworks.Data;
using Coherence.Log;
using Logger = Coherence.Log.Logger;

namespace SteamSample
{
    public class SteamRelay : IRelay, ISocketManager
    {
        private readonly Dictionary<Connection, SteamRelayConnection> connectionMap = new Dictionary<Connection, SteamRelayConnection>();
        private SocketManager steamSocketManager;

        private static readonly Logger logger = Log.GetLogger<SteamRelay>();

        ////////////////////////////////////////////////
        // ICoherenceRelayManager
        ////////////////////////////////////////////////

        public CoherenceRelayManager RelayManager { get; set; }

        public void Open()
        {
            logger.UseWatermark = false;

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
                logger.Info("SteamServer Shutdown");
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
            logger.Info($"OnConnecting {steamConnection.Id}");

            steamConnection.ConnectionName = $"#{info.Identity.SteamId}";
            var relayConnection = new SteamRelayConnection(steamConnection);
            RelayManager.OpenRelayConnection(relayConnection);

            connectionMap.Add(steamConnection, relayConnection);
        }

        public void OnConnected(Connection steamConnection, ConnectionInfo info)
        {
            logger.Info($"OnConnected");
        }

        public void OnDisconnected(Connection steamConnection, ConnectionInfo info)
        {
            logger.Info($"OnDisconnected: {info.State} EndReason: {info.EndReason} ({(int)info.EndReason}) Address: {info.Address} Identity: {info.Identity}");

            if (info.EndReason == NetConnectionEnd.App_Min)
            {
                logger.Info($"Steam client #{steamConnection.Id} closed connection gracefully");
            }

            if (!connectionMap.TryGetValue(steamConnection, out var relayConnection))
            {
                logger.Error($"Failed to find client for connection with Id: {steamConnection.Id}");
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
                logger.Error($"Failed to find client for connection with Id: {steamConnection.Id}");
                return;
            }

            // Push message to the relay connection
            relayConnection.EnqueueMessageFromSteam(new ArraySegment<byte>(packet));
        }
    }
}