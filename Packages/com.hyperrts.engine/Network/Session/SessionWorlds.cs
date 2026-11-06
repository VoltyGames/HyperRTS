using System;
using HyperRTS.Network.Common;
using HyperRTS.Network.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;
using Unity.Networking.Transport;
using UnityEngine;

namespace HyperRTS.Network.Session
{
    /// <summary>Creates the worlds <see cref="NetworkSession"/> runs, with their settings, and opens their connections.</summary>
    internal static class SessionWorlds
    {
        /// <summary>Every server, client and presentation system but the <see cref="RemoteClientOnlyAttribute"/> ones.</summary>
        public static NativeList<SystemTypeIndex> HostSystems()
        {
            var systems = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.ServerSimulation |
                WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation);

            // Removed in place: systems are created in this order, and some look up others in OnCreate.
            for (var i = systems.Length - 1; i >= 0; i--)
            {
                if (Attribute.IsDefined(TypeManager.GetSystemType(systems[i]), typeof(RemoteClientOnlyAttribute)))
                {
                    systems.RemoveAt(i);
                }
            }

            return systems;
        }

        public static NativeList<SystemTypeIndex> ClientSystems() =>
            DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.ClientSimulation |
                WorldSystemFilterFlags.Presentation);

        /// <summary>One world that serves remote clients and is the local player's client, with this player's join.</summary>
        public static NetcodeWorld CreateHost(NativeList<SystemTypeIndex> systems)
        {
            RequireJoinsToSetInGame();
            // Sessions listen and connect themselves, on the port and drivers they were given.
            ClientServerBootstrap.AutoConnectPort = 0;
            var world = ClientServerBootstrap.CreateSingleWorldHost("HostWorld", systems);
            SetSingleton(world, ServerTickRate());
            AddJoin(world);
            return world;
        }

        public static NetcodeWorld CreateServer()
        {
            RequireJoinsToSetInGame();
            ClientServerBootstrap.AutoConnectPort = 0;
            var world = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            SetSingleton(world, ServerTickRate());
            return world;
        }

        public static NetcodeWorld CreateClient(NativeList<SystemTypeIndex> systems)
        {
            RequireJoinsToSetInGame();
            ClientServerBootstrap.AutoConnectPort = 0;
            var world = ClientServerBootstrap.CreateClientWorld("ClientWorld", systems);
            AddJoin(world);
            return world;
        }

        /// <summary>
        /// The join systems put a connection in game once it is seated, so Netcode must not do it on connect: the
        /// global <see cref="NetcodeConfig"/> needs Automatic State Replication off.
        /// </summary>
        private static void RequireJoinsToSetInGame()
        {
            var config = NetcodeConfig.Global;
            if (config == null || config.AutomaticStateReplication)
            {
                throw new InvalidOperationException("[HyperRTS] Turn Automatic State Replication off on the global " +
                                                    "NetcodeConfig asset: joins put connections in game themselves.");
            }
        }

        /// <summary>Opens the port on <paramref name="world"/>; on failure the world is disposed and false returned.</summary>
        public static bool TryListen(NetcodeWorld world, ushort port, INetworkStreamDriverConstructor drivers)
        {
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
                world.Dispose();
            }

            return listening;
        }

        public static void Connect(NetcodeWorld world, NetworkEndpoint server, INetworkStreamDriverConstructor drivers)
        {
            using var query = DriverQuery(world);
            ref var driver = ref query.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            UseDrivers(world, ref driver, drivers);
            driver.Connect(world.EntityManager, server);
        }

        // Netcode seeds its tick rate singletons from NetcodeConfig as it creates a world.
        private static void SetSingleton<T>(World world, T value) where T : unmanaged, IComponentData
        {
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<T>());
            query.SetSingleton(value);
        }

        private static void AddJoin(World world)
        {
            world.EntityManager.CreateSingleton(NetworkSession.Preference);
            world.EntityManager.CreateSingleton(new JoinToken { Value = NetworkSession.Token });
        }

        // Interpolated units look the same at 20 Hz as at 60, for a third of the bandwidth (docs/networking.md#bandwidth).
        public static ClientServerTickRate ServerTickRate()
        {
            var rate = new ClientServerTickRate();
            rate.ResolveDefaults();
            rate.NetworkTickRate = NetworkSession.SnapshotRate;
            return rate;
        }

        private static EntityQuery DriverQuery(World world) =>
            world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());

        // Netcode builds its default drivers with the world; a relay session swaps them before listening or connecting.
        private static void UseDrivers(NetcodeWorld world, ref NetworkStreamDriver driver,
            INetworkStreamDriverConstructor drivers)
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
    }
}
