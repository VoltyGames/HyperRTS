using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Core
{
    /// <summary>World filters for simulation systems: single player, a dedicated server, a host, and clients.</summary>
    public static class SimulationWorlds
    {
        /// <summary>Worlds that own the game state: single player and the server.</summary>
        public const WorldSystemFilterFlags Authoritative =
            WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation;

        /// <summary>Worlds a player looks at: single player and clients.</summary>
        public const WorldSystemFilterFlags Presented =
            WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation;

        public const WorldSystemFilterFlags All = Authoritative | WorldSystemFilterFlags.ClientSimulation;

        /// <summary>Whether <paramref name="world"/> owns the game state: single player, a server or a host.</summary>
        public static bool IsAuthoritative(WorldUnmanaged world) => world.IsServer() || !world.IsClient();
    }
}
