using Unity.Entities;

namespace HyperRTS.Core
{
    /// <summary>World filters for simulation systems: single player, dedicated or hosted server, and clients.</summary>
    public static class SimulationWorlds
    {
        /// <summary>Worlds that own the game state: single player and the server.</summary>
        public const WorldSystemFilterFlags Authoritative =
            WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation;

        /// <summary>Worlds a player looks at: single player and clients.</summary>
        public const WorldSystemFilterFlags Presented =
            WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation;

        public const WorldSystemFilterFlags All = Authoritative | WorldSystemFilterFlags.ClientSimulation;
    }
}
