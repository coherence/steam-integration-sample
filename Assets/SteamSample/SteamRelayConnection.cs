using System;
using System.Collections.Generic;
using Coherence.Toolkit.Relay;
using Steamworks;
using Steamworks.Data;
using Coherence.Log;
using UnityEngine;
using Logger = Coherence.Log.Logger;

namespace SteamSample
{
    public class SteamRelayConnection : IRelayConnection
    {
        private Connection steamConnection;
        private readonly Queue<ArraySegment<byte>> messagesFromSteamToServer = new Queue<ArraySegment<byte>>();
        private readonly byte[] outgoingPacketBuffer = new byte[1024 * 4];

        public SteamRelayConnection(Connection steamConnection)
        {
            Debug.Log($"Opening relayed client for Steam user #{steamConnection.ConnectionName}");

            this.steamConnection = steamConnection;
        }

        public void OnConnectionOpened()
        {
            steamConnection.Accept();
        }

        public void OnConnectionClosed()
        {
            var result = steamConnection.Close();
            if (!result)
            {
                Debug.LogError("Failed to close Steam relay connection.");
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
                Debug.LogError($"Failed to send message to Steam client.\nResult: {result}\nSend Type: {sendType}\nSteam Connection ID: {steamConnection.Id}");
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
