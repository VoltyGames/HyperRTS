using System;
using HyperRTS.Network.Common;
using HyperRTS.Network.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;
using Unity.Netcode.NetcodeTime;
using Unity.Networking.Transport;
using Unity.Physics.Systems;
using UnityEngine;

namespace HyperRTS.Network.Session
{
    /// <summary>Creates the worlds <see cref="NetworkSession"/> runs, with their settings, and opens their connections.</summary>
    internal static class SessionWorlds
    {
        /// <summary>Every server, client and presentation system but the <see cref="RemoteClientOnlyAttribute"/> ones.</summary>
        public static NativeList<SystemTypeIndex> HostSystems()
        {
            var systems = Systems(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.ClientSimulation,
                WorldSystemFilterFlags.Presentation);

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
            Systems(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Presentation);

        /// <summary>
        /// The systems of every role, in the order of the first role that has each. Entities 6.7 drops systems that
        /// lack one of several combined flags, the world time and scene systems among them, so roles go one by one.
        /// </summary>
        private static NativeList<SystemTypeIndex> Systems(params WorldSystemFilterFlags[] roles)
        {
            var systems = new NativeList<SystemTypeIndex>(512, Allocator.Temp);
            var added = new NativeHashSet<SystemTypeIndex>(512, Allocator.Temp);
            foreach (var role in roles)
            {
                foreach (var system in DefaultWorldInitialization.GetAllSystemTypeIndices(role))
                {
                    if (added.Add(system))
                    {
                        systems.Add(system);
                    }
                }
            }

            return systems;
        }

        /// <summary>One world that serves remote clients and is the local player's client, with this player's join.</summary>
        public static NetcodeWorld CreateHost(NativeList<SystemTypeIndex> systems)
        {
            // Sessions listen and connect themselves, on the port and drivers they were given.
            ClientServerBootstrap.AutoConnectPort = 0;
            var world = ClientServerBootstrap.CreateSingleWorldHost("HostWorld", systems);
            SetSingleton(world, ServerTickRate());
            AddJoin(world);
            return world;
        }

        public static NetcodeWorld CreateServer()
        {
            ClientServerBootstrap.AutoConnectPort = 0;
            var world = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            SetSingleton(world, ServerTickRate());
            DisablePhysics(world);
            return world;
        }

        public static NetcodeWorld CreateClient(NativeList<SystemTypeIndex> systems)
        {
            ClientServerBootstrap.AutoConnectPort = 0;
            var world = ClientServerBootstrap.CreateClientWorld("ClientWorld", systems);
            SetSingleton(world, ClientTickRate());
            AddJoin(world);
            return world;
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

        // Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop.
        private static ClientTickRate ClientTickRate()
        {
            var rate = NetworkTimeSystem.DefaultClientTickRate;
            rate.PredictionLoopUpdateMode = PredictionLoopUpdateMode.AlwaysRun;
            return rate;
        }

        // Physics only serves click raycasts, which a dedicated server never makes.
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
