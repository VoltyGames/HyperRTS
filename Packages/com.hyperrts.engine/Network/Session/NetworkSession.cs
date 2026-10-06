using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Starts and stops a networked match: replaces the single-player world with server and/or client worlds and
    /// reloads the active scene so its SubScene streams into them.
    /// </summary>
    public static class NetworkSession
    {
        public const ushort DefaultPort = 7979;

        /// <summary>Slot (faction) to ask for when joining; reconnecting clients get their old slot back.</summary>
        public static byte PreferredFaction { get; set; }

        public static bool IsRunning => ClientServerBootstrap.ServerWorld != null || ClientServerBootstrap.ClientWorld != null;

        /// <summary>Server and client in this process, as for custom lobbies and LAN games.</summary>
        public static void StartHost(ushort port = DefaultPort) =>
            Start(port, NetworkEndpoint.LoopbackIpv4.WithPort(port), server: true, client: true);

        /// <summary>Dedicated server: no local player.</summary>
        public static void StartServer(ushort port = DefaultPort) => Start(port, default, server: true, client: false);

        public static void StartClient(string address, ushort port = DefaultPort)
        {
            if (!NetworkEndpoint.TryParse(address, port, out var endpoint))
            {
                Debug.LogError($"[HyperRTS] Invalid server address '{address}:{port}'.");
                return;
            }

            Start(port, endpoint, server: false, client: true);
        }

        /// <summary>Back to a single-player world.</summary>
        public static void Stop()
        {
            DisposeWorlds();
            DefaultWorldInitialization.Initialize("Default World");
            ReloadScene();
        }

        private static void Start(ushort port, NetworkEndpoint serverEndpoint, bool server, bool client)
        {
            DisposeWorlds();
            if (server)
            {
                var world = ClientServerBootstrap.CreateServerWorld("ServerWorld");
                DisablePhysics(world);
                using var driver = DriverQuery(world);
                driver.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(NetworkEndpoint.AnyIpv4.WithPort(port));
                World.DefaultGameObjectInjectionWorld = world;
            }

            if (client)
            {
                var world = ClientServerBootstrap.CreateClientWorld("ClientWorld");
                world.EntityManager.CreateSingleton(ClientTickRate());
                using var driver = DriverQuery(world);
                driver.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(world.EntityManager, serverEndpoint);
                World.DefaultGameObjectInjectionWorld = world;
            }

            ReloadScene();
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

        private static void ReloadScene()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PreferredFaction = 0;
    }
}
