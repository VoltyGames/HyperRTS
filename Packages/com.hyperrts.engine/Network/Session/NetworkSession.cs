using System;
using System.Collections.Generic;
using HyperRTS.Network.Players;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
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

        /// <summary>Snapshots per second; the simulation still ticks at Netcode's default 60 Hz.</summary>
        public const int SnapshotRate = 20;

        /// <summary>The scene load the last start or <see cref="LoadScene"/> began, for loading screens; null if none.</summary>
        public static AsyncOperation SceneLoad => SessionSceneLoader.Current;

        /// <summary>
        /// Slot (faction) to ask for when joining. Updated to the granted slot when the client world closes, so a
        /// reconnecting client asks for its old slot.
        /// </summary>
        public static byte PreferredFaction { get; set; }

        /// <summary>Join as an observer instead of claiming a slot. Set when an observer's client world closes, so it
        /// rejoins as one.</summary>
        public static bool JoinAsObserver { get; set; }

        /// <summary>This process's identity, sent with every join: the server gives the slot it owns back to it.</summary>
        public static ulong Token { get; private set; } = NewToken();

        /// <summary>Whether <see cref="Rejoin"/> has a direct endpoint: the last session was a direct join.</summary>
        public static bool CanRejoin => _rejoin.IsValid;

        private static NetworkEndpoint _rejoin;

        public static bool IsRunning => ClientServerBootstrap.ServerWorld != null || ClientServerBootstrap.ClientWorld != null;

        public static NetworkStatus Status { get; private set; }

        /// <summary>Why the connection closed, while <see cref="Status"/> is <see cref="NetworkStatus.Disconnected"/>.</summary>
        public static NetworkStreamDisconnectReason DisconnectReason { get; private set; }

        /// <summary>Server and client in this process, as for custom lobbies and LAN games.</summary>
        public static bool StartHost(ushort port = DefaultPort, SessionScene scene = default) =>
            Host(port, null, null, scene);

        /// <summary>
        /// Player-hosted match over a relay allocation the game obtained (Unity Relay, Steam, its own backend):
        /// remote players join through the relay, the local player over IPC.
        /// </summary>
        public static bool StartRelayHost(RelayServerData hostRelay, ushort port = DefaultPort,
            SessionScene scene = default) =>
            Host(port, new RelayDriverConstructor(hostRelay), new RelayDriverConstructor(default), scene);

        /// <summary>Dedicated server: no local player. A port that can't be opened changes nothing and returns false.</summary>
        public static bool StartServer(ushort port = DefaultPort, SessionScene scene = default)
        {
            if (!TryListen(port, null, out var server))
            {
                return false;
            }

            DisposeWorlds(server);
            _rejoin = default;
            World.DefaultGameObjectInjectionWorld = server;
            SetStatus(NetworkStatus.Connected);
            SessionSceneLoader.Load(scene);
            return true;
        }

        public static bool StartClient(string address, ushort port = DefaultPort, SessionScene scene = default)
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
        public static bool StartRelayClient(RelayServerData clientRelay, SessionScene scene = default)
        {
            if (!clientRelay.Endpoint.IsValid)
            {
                Debug.LogError("[HyperRTS] Invalid relay server data.");
                return false;
            }

            Join(clientRelay.Endpoint, new RelayDriverConstructor(clientRelay), scene);
            return true;
        }

        /// <summary>
        /// Joins the server of the last <see cref="StartClient"/> again, as after a dropped connection; the scene must
        /// load the map again, since a SubScene only streams into worlds that exist when it loads. Relay sessions
        /// rejoin through a new allocation and <see cref="StartRelayClient"/>.
        /// </summary>
        public static bool Rejoin(SessionScene scene)
        {
            if (!CanRejoin)
            {
                Debug.LogError("[HyperRTS] Nothing to rejoin: the last session was not a direct join.");
                return false;
            }

            Join(_rejoin, null, scene);
            return true;
        }

        /// <summary>Back to a single-player world, reloading the active scene.</summary>
        public static void Stop() => StartLocal(SessionScene.ReloadActive);

        /// <summary>Replaces every world with a fresh single-player world and loads <paramref name="scene"/> into it.</summary>
        public static void StartLocal(SessionScene scene)
        {
            DisposeWorlds(null);
            _rejoin = default;
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
        }

        private static bool Host(ushort port, INetworkStreamDriverConstructor serverDrivers,
            INetworkStreamDriverConstructor clientDrivers, SessionScene scene)
        {
            if (!TryListen(port, serverDrivers, out var server))
            {
                return false;
            }

            DisposeWorlds(server);
            _rejoin = default;
            World.DefaultGameObjectInjectionWorld = server;
            StartClientWorld(NetworkEndpoint.LoopbackIpv4.WithPort(port), clientDrivers);
            SessionSceneLoader.Load(scene);
            return true;
        }

        private static void Join(NetworkEndpoint server, INetworkStreamDriverConstructor drivers, SessionScene scene)
        {
            DisposeWorlds(null);
            _rejoin = drivers == null ? server : default;
            StartClientWorld(server, drivers);
            SessionSceneLoader.Load(scene);
        }

        /// <summary>
        /// Creates a server world and opens the port, next to the running worlds; on failure the new world is gone
        /// again and nothing else changed.
        /// </summary>
        private static bool TryListen(ushort port, INetworkStreamDriverConstructor drivers, out World world)
        {
            world = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            world.EntityManager.CreateSingleton(SessionWorlds.ServerTickRate());
            SessionWorlds.DisablePhysics(world);
            bool listening;
            using (var query = SessionWorlds.DriverQuery(world))
            {
                ref var driver = ref query.GetSingletonRW<NetworkStreamDriver>().ValueRW;
                SessionWorlds.UseDrivers(world, ref driver, drivers);
                listening = driver.Listen(NetworkEndpoint.AnyIpv4.WithPort(port));
            }

            if (!listening)
            {
                Debug.LogError($"[HyperRTS] Could not listen on port {port}.");
                world.Dispose();
                world = null;
            }

            return listening;
        }

        private static void StartClientWorld(NetworkEndpoint server, INetworkStreamDriverConstructor drivers)
        {
            var world = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            world.EntityManager.CreateSingleton(SessionWorlds.ClientTickRate());
            world.EntityManager.CreateSingleton(new JoinPreference { Faction = PreferredFaction, Observe = JoinAsObserver });
            world.EntityManager.CreateSingleton(new JoinToken { Value = Token });
            using (var query = SessionWorlds.DriverQuery(world))
            {
                ref var driver = ref query.GetSingletonRW<NetworkStreamDriver>().ValueRW;
                SessionWorlds.UseDrivers(world, ref driver, drivers);
                driver.Connect(world.EntityManager, server);
            }

            World.DefaultGameObjectInjectionWorld = world;
            SetStatus(NetworkStatus.Connecting);
        }

        /// <summary>Disposes every simulation world but <paramref name="keep"/>.</summary>
        private static void DisposeWorlds(World keep)
        {
            RememberSlot();
            var worlds = new List<World> { World.DefaultGameObjectInjectionWorld };
            worlds.AddRange(ClientServerBootstrap.ServerWorlds);
            worlds.AddRange(ClientServerBootstrap.ClientWorlds);
            worlds.AddRange(ClientServerBootstrap.ThinClientWorlds);
            foreach (var world in worlds)
            {
                if (world != null && world != keep && world.IsCreated)
                {
                    world.Dispose();
                }
            }
        }

        /// <summary>Keeps the closing client's slot, or its observer seat, for the next join.</summary>
        private static void RememberSlot()
        {
            var client = ClientServerBootstrap.ClientWorld;
            if (client == null || !client.IsCreated)
            {
                return;
            }

            if (!TryRemember(client.EntityManager, out var rejoin))
            {
                return;
            }

            PreferredFaction = rejoin.Faction;
            JoinAsObserver = rejoin.Observe;
        }

        private static bool TryRemember(EntityManager entities, out JoinPreference rejoin)
        {
            using var granted = entities.CreateEntityQuery(ComponentType.ReadOnly<LocalFaction>());
            if (granted.TryGetSingleton(out LocalFaction faction))
            {
                rejoin = JoinPreference.Rejoining(faction);
                return true;
            }

            // Dropped before the server answered: ask again for what this client asked for.
            using var asked = entities.CreateEntityQuery(ComponentType.ReadOnly<JoinPreference>());
            return asked.TryGetSingleton(out rejoin);
        }

        // Never 0, which a join request sends when it owns no slot.
        private static ulong NewToken() => BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0) | 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PreferredFaction = 0;
            JoinAsObserver = false;
            Token = NewToken();
            _rejoin = default;
            Status = NetworkStatus.Idle;
            DisconnectReason = default;
        }
    }
}
