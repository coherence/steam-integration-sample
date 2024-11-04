/*
 using System;
using System.Collections;
using System.Collections.Generic;
using Coherence.Toolkit;
using NUnit.Framework;
using SteamSample;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.TestTools;

public class SteamSample_Works
{

    private SteamId lobbyID;
    private SteamManager steamManager;
    private Lobby[] lobbies = {};

    private CoherenceBridge bridge;
    private CoherenceLiveQuery query;

    [SetUp]
    public void SetUp()
    {
        GameObject sm = new GameObject();
        steamManager = sm.AddComponent<SteamManager>();

        GameObject coherenceObj = new GameObject();
        bridge = coherenceObj.AddComponent<CoherenceBridge>();
        query = coherenceObj.AddComponent<CoherenceLiveQuery>();
    }

    [Test, Ignore("Steam Client needs to be running for this test to be running. ")]
    public void SteamSample_HostLobby_Works()
    {
        steamManager.GetComponent<SteamManager>().HostGame();

        Assert.That(steamManager.activeLobby.HasValue, "Waiting for Steam to host");

        steamManager.Disconnect();

        Assert.That(steamManager.activeLobby.HasValue, "Waiting for Steam to disconnect");
    }

    [Test, Ignore("Steam Client needs to be running for this test to be running. ")]
    public async void SteamSample_JoinLobby_Works()
    {
        // SteamClient must be initialized before we can use SteamMatchmaking
        Assert.That(SteamClient.IsValid, "Lobbies refresh failed: SteamClient not initialized");

        try
        {
            lobbies = await SteamMatchmaking.LobbyList.RequestAsync();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Lobbies refresh failed: {exception}");
        }

        Assert.That(lobbies.Length != 0, "Lobbies were not found");

        bool excThrown = false;

        foreach (var lobby in lobbies)
        {
            try
            {
                steamManager.JoinGame(lobby);
                if (steamManager.activeLobby.HasValue)
                {
                    break;
                }
            }
            catch (NotImplementedException e)
            {
                var exc = e;
                excThrown = true;
            }
        }
        Assert.True(excThrown, "Connection happened, NotImplementedException on connect is expected");

        Assert.That(steamManager.activeLobby.HasValue, "Lobby is connected");

    }

    [TearDown]
    public void TearDown()
    {
        GameObject.Destroy(steamManager.gameObject);
        GameObject.Destroy(bridge.gameObject);
    }

}
*/