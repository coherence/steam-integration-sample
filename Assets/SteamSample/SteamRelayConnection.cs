using System;
using System.Collections.Generic;
using Coherence.Toolkit.Relay;
using Steamworks;
using Steamworks.Data;
using Coherence.Log;
using Logger = Coherence.Log.Logger;

namespace SteamSample
{
    public class SteamRelayConnection : IRelayConnection
    {
        private Connection steamConnection;
        private readonly Queue<ArraySegment<byte>> messagesFromSteamToServer = new Queue<ArraySegment<byte>>();
        private readonly byte[] outgoingPacketBuffer = new byte[1024 * 4];

        private static readonly Logger logger = Log.GetLogger<SteamRelayConnection>();

        public SteamRelayConnection(Connection steamConnection)
        {
            logger.UseWatermark = false;

            logger.Info($"Opening relayed client for Steam user #{steamConnection.ConnectionName}");

            this.steamConnection = steamConnection;
        }

        public void OnConnectionOpened()
        {
            steamConnection.Accept();
        }

        public void OnConnectionClosed()
        {
            logger.Info($"Closing relayed client for Steam user {steamConnection.ConnectionName}");

            var result = steamConnection.Close();
            if (!result)
            {
                logger.Error($"Failed to close Steam relay connection");
            }

            messagesFromSteamToServer.Clear();
        }

        public void SendMessageToClient(ReadOnlySpan<byte> packetData)
        {
            // Throttling is already handled by coherence
            var sendType = SendType.Unreliable | SendType.NoNagle;

            packetData.CopyTo(outgoingPacketBuffer);

            var result = steamConnection.SendMessage(outgoingPacketBuffer, 0, packetData.Length, sendType);
            if (result != Result.OK)
            {
                logger.Error($"Sending message to {steamConnection.ConnectionName} failed with result: {result}");
            }
        }

        public void EnqueueMessageFromSteam(ArraySegment<byte> packetData)
        {
            messagesFromSteamToServer.Enqueue(packetData);
        }

        public void ReceiveMessagesFromClient(List<ArraySegment<byte>> packetBuffer)
        {
            // Transfer packets to the coherence replication server
            while (messagesFromSteamToServer.Count > 0)
            {
                var packetData = messagesFromSteamToServer.Dequeue();
                packetBuffer.Add(packetData);
            }
        }
    }
}
