using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SteamSample
{
    [RequireComponent(typeof(SteamManager))]
    public class SteamLobbyUI : MonoBehaviour
    {
        public bool showLobbyUI = true;

        public SteamManager steamManager;
        public List<Lobby> lobbies = new List<Lobby>();
        public Vector2 scrollBarPosition;
        public bool refreshInProgress;

        void Awake()
        {
            if (steamManager == null)
            {
                steamManager = GetComponent<SteamManager>();
            }
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

        void RefreshLobbies()
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

            refreshInProgress = true;

            // Request lobbies
            SteamMatchmaking
                .LobbyList
                .RequestAsync()
                .ContinueWith(request => {
                    refreshInProgress = false;

                    // Request has failed
                    if (request.IsFaulted)
                    {
                        Debug.LogError($"Lobbies refresh failed: {request.Exception}");
                        return;
                    }

                    // Request succeeded, update the lobbies list
                    lobbies.Clear();
                    lobbies.AddRange(request.Result);
                }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
