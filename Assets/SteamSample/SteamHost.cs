using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Coherence.Connection;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SteamSample
{
    public class SteamHost : ISocketManager
    {
        private readonly EndpointData endpointData;
        private readonly Dictionary<Connection, SteamRelayedClient> clients
            = new Dictionary<Connection, SteamRelayedClient>();

        private readonly SocketManager steamSocketManager;

        public SteamHost(EndpointData endpointData)
        {
            this.endpointData = endpointData;
            var serverInit = new SteamServerInit(Application.productName, Application.productName);

            if (!SteamServer.IsValid)
            {
                SteamServer.Init(SteamClient.AppId, serverInit, false);
                Debug.Log($"{nameof(SteamHost)} initialized");
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

            foreach (var client in clients.Values)
            {
                client.Update();
            }
        }

        public void Close()
        {
            foreach (var client in clients.Values)
            {
                CloseClient(client);
            }
            clients.Clear();

            if (SteamServer.IsValid)
            {
                SteamServer.Shutdown();
            }

            steamSocketManager.Close();
        }

        private void RSConnectionFailed(Connection connection, ConnectionException e)
        {
            Debug.LogError($"#{connection.ConnectionName}: RS Connection error: {e}");
            CloseAndRemoveClient(connection);
        }

        private void CloseAndRemoveClient(Connection connection)
        {
            if (!SteamServer.IsValid)
            {
                // Relay has already shutdown
                return;
            }

            if (!clients.TryGetValue(connection, out var client))
            {
                Debug.LogError($"{nameof(SteamHost)} failed to remove client #{connection.ConnectionName}:");
                return;
            }

            CloseClient(client);

            clients.Remove(connection);
        }

        private void CloseClient(SteamRelayedClient client)
        {
            client.Close();
            client.OnError -= RSConnectionFailed;
        }

        private void AcceptNewClient(Connection connection)
        {
            connection.Accept();
            var client = new SteamRelayedClient(connection, endpointData);
            clients[connection] = client;
            client.OnError += RSConnectionFailed;
        }

        public void OnConnecting(Connection connection, ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamHost)} OnConnecting: {info.State} ID: {connection.Id} SteamID: #{info.Identity.SteamId}");
            connection.ConnectionName = $"#{info.Identity.SteamId}";
            AcceptNewClient(connection);
        }

        public void OnConnected(Connection connection, ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamHost)} OnConnected: {info.State} SteamID: #{info.Identity.SteamId}");
        }

        public void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamHost)} OnDisconnected: {info.State} SteamID: #{info.Identity.SteamId}");
            CloseAndRemoveClient(connection);
        }

        public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            if (!clients.TryGetValue(connection, out SteamRelayedClient client))
            {
                Debug.LogError($"{nameof(SteamHost)} #{connection.ConnectionName} failed to find client for connection with Id: {connection.Id}");
                return;
            }

            // Copy packet data into managed byte-array before relaying to server
            var packet = new byte[size];
            Marshal.Copy(data, packet, 0 , size);

            client.RelayToServer(packet);
        }
    }
}