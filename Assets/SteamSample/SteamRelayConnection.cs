using System;
using System.Collections.Generic;
using Coherence.Toolkit.Relay;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SteamSample
{
    public class SteamRelayConnection : IRelayConnection
    {
        private Connection steamConnection;
        private readonly Queue<ArraySegment<byte>> messagesFromSteamToServer = new Queue<ArraySegment<byte>>();

        public SteamRelayConnection(Connection steamConnection)
        {
            Debug.Log($"{nameof(SteamRelayConnection)} opening relayed client for Steam user #{steamConnection.ConnectionName}");

            this.steamConnection = steamConnection;
        }

        public void OnConnectionOpened()
        {
            steamConnection.Accept();
        }

        public void OnConnectionClosed()
        {
            Debug.Log($"{nameof(SteamRelayConnection)} closing relayed client for Steam user #{steamConnection.ConnectionName}");

            var result = steamConnection.Close();
            if (!result)
            {
                Debug.LogError($"{nameof(SteamRelayConnection)} failed to close Steam relay connection");
            }

            messagesFromSteamToServer.Clear();
        }

        public void SendMessageToClient(ArraySegment<byte> packetData)
        {
            // Throttling is already handled by coherence
            var sendType = SendType.Unreliable | SendType.NoNagle;

            var result = steamConnection.SendMessage(packetData.Array, packetData.Offset, packetData.Count, sendType);
            if (result != Result.OK)
            {
                Debug.LogError($"{nameof(SteamRelayConnection)} sending message to {steamConnection.ConnectionName} failed with result: {result}");
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
