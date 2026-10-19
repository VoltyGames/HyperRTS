using Unity.Entities;
using Unity.NetCode;
using Unity.Physics.Systems;

namespace HyperRTS.Network.Session
{
    /// <summary>Settings <see cref="NetworkSession"/> gives the server and client worlds it creates.</summary>
    internal static class SessionWorlds
    {
        // Interpolated units look the same at 20 Hz as at 60, for a third of the bandwidth (docs/networking.md#bandwidth).
        public static ClientServerTickRate ServerTickRate()
        {
            var rate = new ClientServerTickRate();
            rate.ResolveDefaults();
            rate.NetworkTickRate = NetworkSession.SnapshotRate;
            return rate;
        }

        // Clicks raycast against Unity Physics, which Netcode only steps inside the prediction loop.
        public static ClientTickRate ClientTickRate()
        {
            var rate = NetworkTimeSystem.DefaultClientTickRate;
            rate.PredictionLoopUpdateMode = PredictionLoopUpdateMode.AlwaysRun;
            return rate;
        }

        // Physics only serves click raycasts, which run on clients; the server would rebuild it every tick for nothing.
        public static void DisablePhysics(World world)
        {
            var physics = world.GetExistingSystemManaged<PhysicsSystemGroup>();
            if (physics != null)
            {
                physics.Enabled = false;
            }
        }

        public static EntityQuery DriverQuery(World world) =>
            world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());

        // Netcode builds its default drivers with the world; a relay session swaps them before listening or connecting.
        public static void UseDrivers(World world, ref NetworkStreamDriver driver, INetworkStreamDriverConstructor drivers)
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
