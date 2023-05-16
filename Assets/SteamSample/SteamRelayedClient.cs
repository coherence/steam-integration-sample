using System;
using System.Collections.Generic;
using System.Net;
using Coherence.Brook;
using Coherence.Brook.Octet;
using Coherence.Connection;
using Coherence.Log;
using Coherence.Stats;
using Coherence.Transport;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SteamSample
{
    public class SteamRelayedClient
    {
        public event Action<Connection, ConnectionException> OnError;

        private Connection connection;
        private readonly UdpTransport replicationServerTransport;
        private readonly List<(IInOctetStream, IPEndPoint)> receiveBuffer = new List<(IInOctetStream, IPEndPoint)>();

        public SteamRelayedClient(Connection connection, EndpointData endpointData)
        {
            Debug.Log($"{nameof(SteamRelayedClient)} opening relayed client for Steam user #{connection.ConnectionName}");

            this.connection = connection;

            replicationServerTransport = new UdpTransport(new StubStats(), Log.GetLogger(typeof(UdpTransport)));
            replicationServerTransport.OnError += HandleConnectionError;
            replicationServerTransport.Open(endpointData);
        }

        private void HandleConnectionError(ConnectionException e)
        {
            OnError?.Invoke(connection, e);
        }

        public void RelayToServer(byte[] data)
        {
            var stream = new OutOctetStream();
            stream.WriteOctets(data);
            replicationServerTransport.Send(stream);
        }

        public void Update()
        {
            if (replicationServerTransport.State == TransportState.Closed)
            {
                Debug.LogError($"{nameof(SteamRelayedClient)} failed to update, connection to RS is closed.");
                return;
            }

            receiveBuffer.Clear();
            replicationServerTransport.Receive(receiveBuffer);

            foreach (var (packet, _) in receiveBuffer)
            {
                var buffer = packet.GetBuffer();
                var result = connection.SendMessage(buffer.Array, buffer.Offset, buffer.Count, SendType.Unreliable);
                if (result != Result.OK)
                {
                    Debug.LogError($"{nameof(SteamRelayedClient)} sending message to {connection.ConnectionName} failed with result: {result}");
                }
            }
        }

        public void Close()
        {
            Debug.Log($"{nameof(SteamRelayedClient)} closing relayed client for Steam user #{connection.ConnectionName}");

            replicationServerTransport.Close();
            replicationServerTransport.OnError -= HandleConnectionError;

            var result = connection.Close();
            if (!result)
            {
                Debug.LogError($"{nameof(SteamRelayedClient)} failed to close Steam relay connection");
            }
        }
    }
}
