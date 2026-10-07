using System;
using System.Collections.Generic;
using HyperRTS.Network.Players;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Physics.Systems;
using UnityEngine;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Starts and stops a networked match: replaces the single-player world with server and/or client worlds and
    /// loads a scene (by default the active one, see <see cref="SessionScene"/>) so its SubScene streams into them.
    /// </summary>
    public static class NetworkSession
    {
        public const ushort DefaultPort = 7979;

        /// <summary>The scene load the last start or <see cref="LoadScene"/> began, for loading screens; null if none.</summary>
        public static AsyncOperation SceneLoad => SessionSceneLoader.Current;

        /// <summary>
        /// Slot (faction) to ask for when joining. Updated to the granted slot when the client world closes, so a
        /// reconnecting client gets its old slot back.
        /// </summary>
        public static byte PreferredFaction { get; set; }

        /// <summary>Join as an observer instead of claiming a slot.</summary>
        public static bool JoinAsObserver { get; set; }

        public static bool IsRunning => ClientServerBootstrap.ServerWorld != null || ClientServerBootstrap.ClientWorld != null;

        public static NetworkStatus Status { get; private set; }

        /// <summary>Why the connection closed, while <see cref="Status"/> is <see cref="NetworkStatus.Disconnected"/>.</summary>
        public static NetworkStreamDisconnectReason DisconnectReason { get; private set; }

        public static event Action<NetworkStatus> StatusChanged;

        /// <summary>Server and client in this process, as for custom lobbies and LAN games.</summary>
        public static bool StartHost(ushort port = DefaultPort) => StartHost(port, SessionScene.ReloadActive);

        public static bool StartHost(ushort port, SessionScene scene) => Host(port, null, null, scene);

        /// <summary>
        /// Player-hosted match over a relay allocation the game obtained (Unity Relay, Steam, its own backend):
        /// remote players join through the relay, the local player over IPC.
        /// </summary>
        public static bool StartRelayHost(RelayServerData hostRelay, ushort port = DefaultPort) =>
            StartRelayHost(hostRelay, port, SessionScene.ReloadActive);

        public static bool StartRelayHost(RelayServerData hostRelay, ushort port, SessionScene scene) =>
            Host(port, new RelayDriverConstructor(hostRelay), new RelayDriverConstructor(default), scene);

        /// <summary>Dedicated server: no local player.</summary>
        public static bool StartServer(ushort port = DefaultPort) => StartServer(port, SessionScene.ReloadActive);

        public static bool StartServer(ushort port, SessionScene scene)
        {
            DisposeWorlds();
            if (!StartServerWorld(port, null, scene))
            {
                return false;
            }

            SetStatus(NetworkStatus.Connected);
            SessionSceneLoader.Load(scene);
            return true;
        }

        public static bool StartClient(string address, ushort port = DefaultPort) =>
            StartClient(address, port, SessionScene.ReloadActive);

        public static bool StartClient(string address, ushort port, SessionScene scene)
        {
            if (!NetworkEndpoint.TryParse(address, port, out var endpoint))
            {
                Debug.LogError($"[HyperRTS] Invalid server address '{address}:{port}'.");
                return false;
            }

            Join(endpoint, null, scene);
            return true;
        }

        /// <summary>Joins a player-hosted match through the relay allocation the game joined with the host's code.</summary>
        public static bool StartRelayClient(RelayServerData clientRelay) =>
            StartRelayClient(clientRelay, SessionScene.ReloadActive);

        public static bool StartRelayClient(RelayServerData clientRelay, SessionScene scene)
        {
            if (!clientRelay.Endpoint.IsValid)
            {
                Debug.LogError("[HyperRTS] Invalid relay server data.");
                return false;
            }

            Join(clientRelay.Endpoint, new RelayDriverConstructor(clientRelay), scene);
            return true;
        }

        /// <summary>Back to a single-player world, reloading the active scene.</summary>
        public static void Stop() => StartLocal(SessionScene.ReloadActive);

        /// <summary>Replaces every world with a fresh single-player world and loads <paramref name="scene"/> into it.</summary>
        public static void StartLocal(SessionScene scene)
        {
            DisposeWorlds();
            DefaultWorldInitialization.Initialize("Default World");
            SetStatus(NetworkStatus.Idle);
            SessionSceneLoader.Load(scene);
        }

        /// <summary>Loads a map into the running session's worlds, as when a lobby that started with
        /// <see cref="SessionScene.Keep"/> launches its match.</summary>
        public static void LoadScene(string path) => SessionSceneLoader.Load(SessionScene.Load(path));

        internal static void SetStatus(NetworkStatus status, NetworkStreamDisconnectReason reason = default)
        {
            if (status == Status && reason == DisconnectReason)
            {
                return;
            }

            Status = status;
            DisconnectReason = reason;
            StatusChanged?.Invoke(status);
        }

        private static bool Host(ushort port, INetworkStreamDriverConstructor serverDrivers,
            INetworkStreamDriverConstructor clientDrivers, SessionScene scene)
        {
            DisposeWorlds();
            if (!StartServerWorld(port, serverDrivers, scene))
            {
                return false;
            }

            StartClientWorld(NetworkEndpoint.LoopbackIpv4.WithPort(port), clientDrivers);
            SessionSceneLoader.Load(scene);
            return true;
        }

        private static void Join(NetworkEndpoint server, INetworkStreamDriverConstructor drivers, SessionScene scene)
        {
            DisposeWorlds();
            StartClientWorld(server, drivers);
            SessionSceneLoader.Load(scene);
        }

        /// <summary>Creates the server world and listens; goes back to single player when that fails.</summary>
        private static bool StartServerWorld(ushort port, INetworkStreamDriverConstructor drivers, SessionScene scene)
        {
            var world = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            DisablePhysics(world);
            World.DefaultGameObjectInjectionWorld = world;
            bool listening;
            using (var query = DriverQuery(world))
            {
                ref var driver = ref query.GetSingletonRW<NetworkStreamDriver>().ValueRW;
                UseDrivers(world, ref driver, drivers);
                listening = driver.Listen(NetworkEndpoint.AnyIpv4.WithPort(port));
            }

            if (!listening)
            {
                Debug.LogError($"[HyperRTS] Could not listen on port {port}.");
                StartLocal(scene);
            }

            return listening;
        }

        private static void StartClientWorld(NetworkEndpoint server, INetworkStreamDriverConstructor drivers)
        {
            var world = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            world.EntityManager.CreateSingleton(ClientTickRate());
            world.EntityManager.CreateSingleton(new JoinPreference { Faction = PreferredFaction, Observe = JoinAsObserver });
            using (var query = DriverQuery(world))
            {
                ref var driver = ref query.GetSingletonRW<NetworkStreamDriver>().ValueRW;
                UseDrivers(world, ref driver, drivers);
                driver.Connect(world.EntityManager, server);
            }

            World.DefaultGameObjectInjectionWorld = world;
            SetStatus(NetworkStatus.Connecting);
        }

        // Netcode builds its default drivers with the world; a relay session swaps them before listening or connecting.
        private static void UseDrivers(World world, ref NetworkStreamDriver driver, INetworkStreamDriverConstructor drivers)
        {
            if (drivers == null)
            {
                return;
            }

            using var netDebugQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetDebug>());
            var netDebug = netDebugQuery.GetSingleton<NetDebug>();
            var store = new NetworkDriverStore();
            if (world.IsServer())
            {
                drivers.CreateServerDriver(world, ref store, netDebug);
            }
            else
            {
                drivers.CreateClientDriver(world, ref store, netDebug);
            }

            driver.ResetDriverStore(world.Unmanaged, ref store);
        }

        // Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop.
        private static ClientTickRate ClientTickRate()
        {
            var rate = NetworkTimeSystem.DefaultClientTickRate;
            rate.PredictionLoopUpdateMode = PredictionLoopUpdateMode.AlwaysRun;
            return rate;
        }

        // Physics only serves click raycasts, which run on clients; the server would rebuild it every tick for nothing.
        private static void DisablePhysics(World world)
        {
            var physics = world.GetExistingSystemManaged<PhysicsSystemGroup>();
            if (physics != null)
            {
                physics.Enabled = false;
            }
        }

        private static EntityQuery DriverQuery(World world) =>
            world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());

        private static void DisposeWorlds()
        {
            RememberSlot();
            var worlds = new List<World> { World.DefaultGameObjectInjectionWorld };
            worlds.AddRange(ClientServerBootstrap.ServerWorlds);
            worlds.AddRange(ClientServerBootstrap.ClientWorlds);
            worlds.AddRange(ClientServerBootstrap.ThinClientWorlds);
            foreach (var world in worlds)
            {
                if (world != null && world.IsCreated)
                {
                    world.Dispose();
                }
            }
        }

        private static void RememberSlot()
        {
            var client = ClientServerBootstrap.ClientWorld;
            if (client == null || !client.IsCreated)
            {
                return;
            }

            using var query = client.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalFaction>());
            if (query.TryGetSingleton(out LocalFaction granted))
            {
                PreferredFaction = granted.Value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PreferredFaction = 0;
            JoinAsObserver = false;
            Status = NetworkStatus.Idle;
            DisconnectReason = default;
            StatusChanged = null;
        }
    }
}
