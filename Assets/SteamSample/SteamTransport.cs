﻿// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using Coherence.Brook;
using Coherence.Brook.Octet;
using Coherence.Common;
using Coherence.Connection;
using Coherence.Log;
using Coherence.Stats;
using Coherence.Transport;
using Steamworks;
using Steamworks.Data;
using Logger = Coherence.Log.Logger;

namespace SteamSample
{
    public class SteamTransport : ITransport, IConnectionManager
    {
        // We need header size matching the header size on the host side, otherwise
        // we risk sending packets bigger than MTU allowed between host and RS.
        internal const int HeaderSizeBytes = UdpTransport.HeaderSizeBytes;

        public SteamId HostSteamId;

        public int HeaderSize => HeaderSizeBytes;
        public event Action OnOpen;
        public event Action<ConnectionException> OnError;

        public TransportState State { get; private set; }
        public bool IsReliable => false;
        public bool CanSend => true;
        public string Description => "Steam";

        private readonly IStats stats;
        private readonly Logger logger;
        private ConnectionManager steamRelayConnection;
        private readonly Queue<byte[]> incomingPackets = new Queue<byte[]>();
        private bool isClosing;

        public SteamTransport(IStats stats, Logger logger)
        {
            this.stats = stats;
            this.logger = logger.With<SteamTransport>();
            isClosing = false;
        }

        public void Open(EndpointData _, ConnectionSettings __)
        {
            if (!SteamClient.IsValid)
            {
                throw new Exception("SteamClient not initialized");
            }

            logger.Info($"Opening outgoing Steam connection.");

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
            logger.Info("Closing SteamTransport");

            if (State == TransportState.Closed)
            {
                logger.Warning(Warning.SteamTransportAlreadyClosed);
                return;
            }

            State = TransportState.Closed;

            steamRelayConnection.Close(true);
        }

        public void Send(IOutOctetStream stream)
        {
            // Disconnect packet needs to be sent reliably, otherwise it will be discarded when the connection is closed
            var sendType = isClosing ? SendType.Reliable : SendType.Unreliable;

            // Throttling is already handled by coherence
            sendType |= SendType.NoNagle;

            var buffer = stream.Close();
            var result = steamRelayConnection.Connection.SendMessage(buffer.Array, buffer.Offset, buffer.Count, sendType);
            if (result != Result.OK)
            {
                logger.Error(Error.SteamFailedToSendPacket,
                    ("Result", result),
                    ("TargetSteamId", HostSteamId));
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
            logger.Info($"OnConnecting: {info.State}");
        }

        public void OnConnected(ConnectionInfo info)
        {
            logger.Info($"OnConnected: {info.State}");
            State = TransportState.Open;
        }

        public void OnDisconnected(ConnectionInfo info)
        {
            var gracefulDisconnect = info.EndReason == NetConnectionEnd.App_Min;
            if (gracefulDisconnect)
            {
                // ConnectionDeniedException translates to serverInitiated=true in ClientCore
                // This prevents ClientCore from trying and failing to send a DisconnectRequest
                logger.Info($"OnDisconnected: Connection closed by host");
                OnError?.Invoke(new ConnectionDeniedException(ConnectionCloseReason.GracefulClose));
            }
            else
            {
                logger.Info($"OnDisconnected: {info.State}: {SteamConnectionException.GetEndReasonString(info)} ({(int)info.EndReason})");
                OnError?.Invoke(new SteamConnectionException(info));
            }
        }

        public void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            // Copy packet data into managed byte array
            var packet = new byte[size];
            Marshal.Copy(data, packet, 0, size);

            incomingPackets.Enqueue(packet);
        }
    }
}
