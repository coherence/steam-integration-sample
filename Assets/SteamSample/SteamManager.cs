using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coherence;
using Coherence.Connection;
using Coherence.Toolkit;
using Coherence.Toolkit.ReplicationServer;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Coherence.Log;
using Logger = Coherence.Log.Logger;

namespace SteamSample
{
    public class SteamManager : MonoBehaviour
    {
        [Tooltip("Copy paste your AppId from https://partner.steamgames.com/")]
        public uint steamAppId;

        [Tooltip("Enter the SteamID of another player whose game you want to join")]
        public ulong steamIdToJoin;

        public Lobby? activeLobby;

        CoherenceBridge bridge;
        EndpointData endpointData;
        bool hostWithLobby;
        IReplicationServer replicationServer;

        private static readonly Logger logger = Log.GetLogger<SteamManager>();
        private static readonly Logger rsLogger = Log.GetLogger<ReplicationServer>();

        void Start()
        {
            // Make sure the scene contains a CoherenceBridge
            if (!CoherenceBridgeStore.TryGetBridge(gameObject.scene, out bridge))
            {
                throw new Exception("Could not find a CoherenceBridge in the scene.");
            }

            logger.UseWatermark = false;
            rsLogger.UseWatermark = false;

            // Listen for connection events
            bridge.onConnected.AddListener(OnConnected);
            bridge.onDisconnected.AddListener(OnDisconnected);
            bridge.onConnectionError.AddListener(OnConnectionError);

            // Create an endpoint for the replication server (default is 127.0.0.1:32001)
            // You can configure the replication server endpoint from the coherence Project Settings
            InitEndpoint();

            // Make sure the Steam API has been initialized before using it
            InitSteamAPI();

            // Join or host a game automatically, if CLI arguments were supplied
            ReadCommandLineArguments();
        }

        void InitEndpoint()
        {
            endpointData = new EndpointData
            {
                host = RuntimeSettings.Instance.LocalHost,
                port = RuntimeSettings.Instance.LocalWorldUDPPort,
                region = EndpointData.LocalRegion,
                schemaId = RuntimeSettings.Instance.SchemaID,
            };

            // Validate the endpoint
            var (valid, error) = endpointData.Validate();
            if (!valid)
            {
                throw new Exception($"Invalid {nameof(EndpointData)}: {error}");
            }
        }

        void InitSteamAPI()
        {
            // Make sure the Steam API has been initialized
            if (steamAppId == default)
            {
                throw new Exception("You need to enter your Steam AppId in the inspector.");
            }

            // Make sure the user is logged in to Steam and that the user owns the AppId.
            if (!SteamClient.IsValid)
            {
                SteamClient.Init(steamAppId, false);
                Dispatch.OnException += exception => logger.Error($"Internal SteamAPI exception: {exception}");
                SteamNetworking.AllowP2PPacketRelay(true); // Enable relay, if NAT punchthrough fails
                SteamNetworking.OnP2PConnectionFailed += (sid, err) => logger.Error($"P2P Connection Failed: {sid} {err}");
                SteamNetworkingUtils.InitRelayNetworkAccess();
            }

            logger.Info($"You are logged in to Steam with SteamID #{SteamClient.SteamId}");
        }

        void Update()
        {
            // SteamClient in non-async mode requires explicit callbacks invoke
            if (SteamClient.IsValid)
            {
                SteamClient.RunCallbacks();
            }
        }

        void OnDisable()
        {
            // Cleanup
            if (bridge)
            {
                bridge.Disconnect();
            }

            Shutdown();
            SteamClient.Shutdown();
        }

        void OnDestroy()
        {
            if(replicationServer != null)
            {
                replicationServer.Stop();
            }
        }

        void Shutdown()
        {
            if (activeLobby.HasValue)
            {
                activeLobby.Value.Leave();
                activeLobby = null;
            }

            if (SteamServer.IsValid)
            {
                SteamServer.Shutdown();
            }
        }

        public void JoinGame(SteamId? steamId = null)
        {
            if (steamId.HasValue)
            {
                steamIdToJoin = steamId.Value;
            }

            logger.Info($"Joining game with SteamID #{steamIdToJoin}");

            hostWithLobby = false;

            if (steamIdToJoin == default)
            {
                throw new Exception($"You need to enter a host SteamId #{steamIdToJoin} in the inspector or CLI");
            }

            // Make sure we are not already in a game or joining a game
            if (bridge.IsConnected || bridge.IsConnecting)
            {
                throw new Exception("Failed to join game, CoherenceBridge is already connected.");
            }

            // Connect to Replication Server via Steam relay
            bridge.SetTransportFactory(new SteamTransportFactory(steamIdToJoin));
            bridge.Connect(endpointData);
        }

        public void JoinGame(Lobby lobby)
        {
            uint ip = 0;
            ushort port = 0;
            SteamId serverId = default;

            // Get the game server SteamID for that lobby and join
            if (lobby.GetGameServer(ref ip, ref port, ref serverId))
            {
                lobby.Join();
                activeLobby = lobby;
                JoinGame(serverId);
            }
            else
            {
                logger.Error($"Failed to get game server SteamID for lobby {lobby.Id}");
            }
        }

        public void HostGame(bool withLobby)
        {
            StartReplicationServer();

            this.hostWithLobby = withLobby;

            logger.Info($"Hosting game with SteamID #{SteamClient.SteamId}");

            // Make sure we are not already hosting or joining a game
            if (bridge.IsConnected || bridge.IsConnecting)
            {
                throw new Exception("Failed to host game, CoherenceBride is already connected.");
            }

            try
            {
                // Init SteamServer
                var serverInit = new SteamServerInit(Application.productName, Application.productName);
                SteamServer.Init(SteamClient.AppId, serverInit, false);
                logger.Info($"SteamServer initialized");
            } catch (Exception e)
            {
                logger.Error(e.ToString());
                return;
            }

            // Init Steam Relay
            bridge.SetRelay(new SteamRelay());

            // Connect to Replication Server using the normal UDP transport
            bridge.SetTransportFactory(null);
            bridge.Connect(endpointData);
        }

        [ContextMenu("Host Game")]
        public void HostGame()
        {
            HostGame(true);
        }

        [ContextMenu("Join Game")]
        public void JoinGame()
        {
            JoinGame(null);
        }

        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            logger.Info("Disconnecting");

            if (!bridge.IsConnected && !bridge.IsConnecting)
            {
                throw new Exception("Failed to disconnect, CoherenceBridge is not connected");
            }

            bridge.Disconnect();

            if (replicationServer != null)
            {
                StopReplicationServer();
            }
        }

        void OnConnected(CoherenceBridge _)
        {
            logger.Info("CoherenceBridge OnConnected");
            if (hostWithLobby)
            {
                CreateLobby();
            }
        }

        void OnDisconnected(CoherenceBridge _, ConnectionCloseReason reason)
        {
            logger.Info($"CoherenceBridge OnDisconnected: {reason}");
            Shutdown();
        }

        void OnConnectionError(CoherenceBridge _, ConnectionException exception)
        {
            logger.Error($"CoherenceBridge OnConnectionError: {exception}");
            Shutdown();
        }

        void CreateLobby()
        {
            logger.Info($"Creating lobby");

            SteamMatchmaking
                .CreateLobbyAsync()
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        logger.Error($"Lobby creation failed: {task.Exception}");
                        return;
                    }

                    Lobby? lobby = task.Result;
                    if (!lobby.HasValue)
                    {
                        logger.Error($"Lobby creation failed");
                        return;
                    }

                    activeLobby = lobby;

                    logger.Info($"Lobby created successfully");

                    lobby.Value.SetGameServer(SteamClient.SteamId);
                    lobby.Value.SetJoinable(true);
                    lobby.Value.SetPublic();
                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        void ReadCommandLineArguments()
        {
            var cliArgs = Environment.GetCommandLineArgs();
            if (cliArgs.Contains("--steam-host"))
            {
                HostGame(true);
            }
            else if (cliArgs.Contains("--steam-join"))
            {
                var hostSteamIdStr = cliArgs.SkipWhile(arg => arg != "--steam-join").Skip(1).Take(1).FirstOrDefault();
                if (!ulong.TryParse(hostSteamIdStr, out var hostId))
                {
                    throw new Exception($"Failed to parse SteamID argument for '--steam-join': {hostSteamIdStr}");
                }

                steamIdToJoin = hostId;
                JoinGame();
            }
        }

        void StartReplicationServer()
        {
            if (replicationServer != null)
            {
                logger.Warning("The replication server is already running");
                return;
            }

            var config = new ReplicationServerConfig
            {
                Mode = Mode.World,
                APIPort = (ushort)RuntimeSettings.Instance.WorldsAPIPort,
                UDPPort = 32001,
                SignallingPort = 32002,
                SendFrequency = 20,
                ReceiveFrequency = 60,
                Token = RuntimeSettings.Instance.ReplicationServerToken,
                DisableThrottling = true,
            };

            var consoleLogDir = Path.GetDirectoryName(Application.consoleLogPath);
            var logFilePath = Path.Combine(consoleLogDir, "coherence-server");
            replicationServer = Launcher.Create(config, $"--log-file \"{logFilePath}\"");
            replicationServer.OnLog += ReplicationServer_OnLog;
            replicationServer.OnExit += ReplicationServer_OnExit;
            replicationServer.Start();
        }

        void StopReplicationServer()
        {
            replicationServer.Stop();
            replicationServer = null;
        }

        void ReplicationServer_OnLog(string log)
        {
            rsLogger.Info(log);
        }

        void ReplicationServer_OnExit(int code)
        {
            logger.Info($"Replication server exited with code {code}.");
            replicationServer = null;
        }
    }
}
