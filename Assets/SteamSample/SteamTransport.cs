// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using Coherence.Brook;
using Coherence.Brook.Octet;
using Coherence.Common;
using Coherence.Connection;
using Coherence.Stats;
using Coherence.Transport;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Logger = Coherence.Log.Logger;

namespace SteamSample
{
    public class SteamTransport : ITransport, IConnectionManager
    {
        public SteamId HostSteamId;

        public event Action OnOpen;
        public event Action<ConnectionException> OnError;

        public TransportState State { get; private set; }
        public bool IsReliable => false;
        public bool CanSend => true;

        private readonly IStats stats;
        private readonly Logger logger;
        private ConnectionManager steamRelayConnection;
        private readonly Queue<byte[]> incomingPackets = new Queue<byte[]>();
        private bool isClosing;

        public SteamTransport(IStats stats, Logger logger)
        {
            this.stats = stats;
            this.logger = logger;
            isClosing = false;
        }

        public void Open(EndpointData _, ConnectionSettings __)
        {
            if (!SteamClient.IsValid)
            {
                throw new Exception("SteamClient not initialized");
            }

            logger.Info($"{nameof(SteamTransport)} opening outgoing Steam connection.");

            steamRelayConnection = SteamNetworkingSockets.ConnectRelay(HostSteamId, 0, this);

            State = TransportState.Opening;

            OnOpen?.Invoke();
        }

        public void PrepareDisconnect()
        {
            isClosing = true;
        }

        public void Close()
        {
            State = TransportState.Closed;

            steamRelayConnection.Close(true);
        }

        public void Send(IOutOctetStream stream)
        {
            // Disconnect packet needs to be sent reliably, otherwise it will be discarded when the connection is closed
            var sendType = isClosing ? SendType.Reliable : SendType.Unreliable;

            // Throttling is already handled by coherence
            sendType |= SendType.NoNagle;

            var buffer = stream.GetBuffer();
            var result = steamRelayConnection.Connection.SendMessage(buffer.Array, buffer.Offset, buffer.Count, sendType);
            if (result != Result.OK)
            {
                logger.Error($"{nameof(SteamTransport)} failed to send Steam packet to #{HostSteamId} with result: {result}");
            }

            stats.TrackOutgoingPacket(stream.Position);
        }

        public void Receive(List<(IInOctetStream, IPEndPoint)> buffer)
        {
            if (!SteamClient.IsValid)
            {
                OnError?.Invoke(new ConnectionException($"SteamClient is not valid"));
                return;
            }

            steamRelayConnection.Receive();

            while (incomingPackets.Count > 0)
            {
                var packet = incomingPackets.Dequeue();
                var stream = new InOctetStream(packet);
                buffer.Add((stream, default));
                stats.TrackIncomingPacket((uint)stream.RemainingOctetCount);
            }
        }

        public void OnConnecting(ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamTransport)} OnConnecting: {info.State}");
        }

        public void OnConnected(ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamTransport)} OnConnected: {info.State}");
            State = TransportState.Open;
        }

        public void OnDisconnected(ConnectionInfo info)
        {
            Debug.Log($"{nameof(SteamTransport)} OnDisconnected: {info.State}");

            OnError?.Invoke(new ConnectionDeniedException(ConnectionCloseReason.Unknown, $"{nameof(SteamTransport)} Peer disconnected: {info.State} EndReason: {info.EndReason}"));
        }

        public void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            // Copy packet data into managed byte array
            var packet = new byte[size];
            Marshal.Copy(data, packet, 0 , size);

            incomingPackets.Enqueue(packet);
        }
    }
}
