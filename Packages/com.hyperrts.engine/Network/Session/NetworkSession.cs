using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HyperRTS.Network.Players;
using Unity.Entities;
using Unity.Netcode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

[assembly: InternalsVisibleTo("HyperRTS.Simulation.Tests")]

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Starts and stops a networked match: replaces the single-player world with a host, server or client world and
    /// loads a scene (by default the active one, see <see cref="SessionScene"/>) so its SubScene streams into them.
    /// </summary>
    public static partial class NetworkSession
    {
        public const ushort DefaultPort = 7979;

        /// <summary>Snapshots per second; the simulation still ticks at Netcode's default 60 Hz.</summary>
        public const int SnapshotRate = 20;

        /// <summary>The scene load the last start or <see cref="LoadScene"/> began, for loading screens; null if none.</summary>
        public static AsyncOperation SceneLoad => SessionSceneLoader.Current;

        /// <summary>
        /// The slot, or observer seat, to ask for when joining. Updated to what was granted when the client world
        /// closes, so a reconnecting client asks for its old one.
        /// </summary>
        [NoAutoStaticsCleanup]
        public static JoinPreference Preference { get; set; }

        /// <summary>This process's identity, sent with every join: the server gives the slot it owns back to it.</summary>
        [NoAutoStaticsCleanup]
        public static ulong Token { get; private set; } = NewToken();

        /// <summary>Whether <see cref="Rejoin"/> has a direct endpoint: the last session was a direct join.</summary>
        public static bool CanRejoin => _rejoin.IsValid;

        [NoAutoStaticsCleanup]
        private static NetworkEndpoint _rejoin;

        /// <summary>The emptied world at the bottom of Unity's active world stack; see <see cref="Empty"/>.</summary>
        private static World _base;

        public static bool IsRunning => ClientServerBootstrap.ServerWorld != null || ClientServerBootstrap.ClientWorld != null;

        [NoAutoStaticsCleanup]
        public static NetworkStatus Status { get; private set; }

        /// <summary>Why the connection closed, while <see cref="Status"/> is <see cref="NetworkStatus.Disconnected"/>.</summary>
        [NoAutoStaticsCleanup]
        public static NetworkStreamDisconnectReason DisconnectReason { get; private set; }

        /// <summary>One world that serves remote clients and plays the local player, as for custom lobbies and LAN games.</summary>
        public static bool StartHost(ushort port = DefaultPort, SessionScene scene = default) =>
            Host(port, null, scene);

        /// <summary>
        /// Player-hosted match over a relay allocation the game obtained (Unity Relay, Steam, its own backend): remote
        /// players join through the relay.
        /// </summary>
        public static bool StartRelayHost(RelayServerData hostRelay, ushort port = DefaultPort,
            SessionScene scene = default) =>
            Host(port, new RelayDriverConstructor(hostRelay), scene);

        /// <summary>Dedicated server: no local player. A port that can't be opened changes nothing and returns false.</summary>
        public static bool StartServer(ushort port = DefaultPort, SessionScene scene = default) =>
            Listen(SessionWorlds.CreateServer(), port, null, scene);

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

        private static bool Host(ushort port, INetworkStreamDriverConstructor drivers, SessionScene scene) =>
            Listen(SessionWorlds.CreateHost(SessionWorlds.HostSystems()), port, drivers, scene);

        /// <summary>Opens the port on a new world that replaces the running ones; on failure they stay untouched.</summary>
        private static bool Listen(NetcodeWorld world, ushort port, INetworkStreamDriverConstructor drivers,
            SessionScene scene)
        {
            if (!SessionWorlds.TryListen(world, port, drivers))
            {
                return false;
            }

            DisposeWorlds(world);
            World.DefaultGameObjectInjectionWorld = world;
            SetStatus(NetworkStatus.Connected);
            SessionSceneLoader.Load(scene);
            return true;
        }

        private static void Join(NetworkEndpoint server, INetworkStreamDriverConstructor drivers, SessionScene scene)
        {
            DisposeWorlds(null);
            if (drivers == null)
            {
                _rejoin = server;
            }

            var world = SessionWorlds.CreateClient(SessionWorlds.ClientSystems());
            SessionWorlds.Connect(world, server, drivers);
            World.DefaultGameObjectInjectionWorld = world;
            SetStatus(NetworkStatus.Connecting);
            SessionSceneLoader.Load(scene);
        }

        /// <summary>Disposes every simulation world but <paramref name="keep"/>, and forgets the rejoin endpoint.</summary>
        private static void DisposeWorlds(World keep)
        {
            RememberSlot();
            _rejoin = default;
            var worlds = new HashSet<World> { World.DefaultGameObjectInjectionWorld };
            worlds.UnionWith(ClientServerBootstrap.ServerWorlds);
            worlds.UnionWith(ClientServerBootstrap.ClientWorlds);
            worlds.UnionWith(ClientServerBootstrap.ThinClientWorlds);
            worlds.RemoveWhere(world => world == null || world == keep || world == _base || !world.IsCreated);

            // Unity disposes only worlds off its active world stack, which pops from the top down.
            while (worlds.Contains(World.PeekWorldStack()))
            {
                var top = World.PeekWorldStack();
                if (!TryPop(top))
                {
                    Empty(top);
                    worlds.Remove(top);
                    break;
                }
            }

            foreach (var world in worlds)
            {
                world.Dispose();
            }
        }

        private static bool TryPop(World world)
        {
            try
            {
                World.PopWorldStack(world);
                return true;
            }
            catch (InvalidOperationException)
            {
                // The world at the bottom of the stack never pops.
                return false;
            }
        }

        /// <summary>
        /// Stops and clears the world Unity keeps at the bottom of its active world stack, which can be neither
        /// popped nor disposed, and keeps it as the empty base the session's worlds stack on.
        /// </summary>
        private static void Empty(World world)
        {
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
            world.DestroyAllSystemsAndLogException();
            world.EntityManager.DestroyEntity(world.EntityManager.UniversalQuery);
            _base = world;
        }

        /// <summary>Keeps the closing client's slot, or its observer seat, for the next join.</summary>
        private static void RememberSlot()
        {
            var client = ClientServerBootstrap.ClientWorld;
            if (client == null || !client.IsCreated)
            {
                return;
            }

            if (TryRemember(client.EntityManager, out var rejoin))
            {
                Preference = rejoin;
            }
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

        [OnEnteringPlayMode]
        internal static void ResetStatics()
        {
            Preference = default;
            Token = NewToken();
            _rejoin = default;
            _base = null;
            Status = NetworkStatus.Idle;
            DisconnectReason = default;
        }
    }
}
