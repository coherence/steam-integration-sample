using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SteamSample
{
    [RequireComponent(typeof(SteamManager))]
    public class SteamLobbyUI : MonoBehaviour
    {
        public bool showLobbyUI = true;

        SteamManager steamManager;
        List<Lobby> lobbies = new List<Lobby>();
        Vector2 scrollBarPosition;
        bool refreshInProgress;

        void Awake()
        {
            steamManager = GetComponent<SteamManager>();
        }

        void OnGUI()
        {
            showLobbyUI = GUILayout.Toggle(showLobbyUI, "Show Lobby UI");
            if (!showLobbyUI)
            {
                return;
            }

            DrawMenu();
            DrawLobbies();
        }

        void DrawMenu()
        {
            if (steamManager.activeLobby.HasValue)
            {
                GUILayout.Label($"Active lobby: {steamManager.activeLobby.Value.Id}");

                if (GUILayout.Button("Disconnect"))
                {
                    steamManager.Disconnect();
                }

                return;
            }

            if (GUILayout.Button("Host"))
            {
                steamManager.HostGame();
            }

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(refreshInProgress ? "Refreshing..." : "Refresh"))
                {
                    RefreshLobbies();
                }
            }
            GUILayout.EndHorizontal();
        }

        

        void DrawLobbies()
        {
            if (steamManager.activeLobby.HasValue)
            {
                return;
            }

            GUILayout.Label("Lobbies:");

            scrollBarPosition = GUILayout.BeginScrollView(scrollBarPosition, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.MinWidth(230));
            {
                foreach (var lobby in lobbies)
                {
                    if (GUILayout.Button($"{lobby.Id} {lobby.MemberCount}/{lobby.MaxMembers}"))
                    {
                        steamManager.JoinGame(lobby);
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        async void RefreshLobbies()
        {
            // Don't refresh if the last refresh is still in progress
            if (refreshInProgress)
            {
                return;
            }

            // SteamClient must be initialized before we can use SteamMatchmaking
            if (!SteamClient.IsValid)
            {
                Debug.LogError("Lobbies refresh failed: SteamClient not initialized");
                return;
            }


            // Request lobbies
            refreshInProgress = true;
            
            try
            {
                Lobby[] newLobbies = await SteamMatchmaking.LobbyList.RequestAsync();
                
                // Request succeeded, update the lobbies list
                lobbies.Clear();
                if (newLobbies != null)
                {
                    lobbies.AddRange(newLobbies);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Lobbies refresh failed: {exception}");
            }
            
            refreshInProgress = false;
        }
    }
}
