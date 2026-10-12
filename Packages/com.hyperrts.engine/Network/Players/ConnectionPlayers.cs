using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Network.Players
{
    /// <summary>Server: which faction a connection plays, for the send systems that filter by viewer.</summary>
    public static class ConnectionPlayers
    {
        /// <summary>The connection's faction; observers (no slot) are neutral, whose team 0 sees and hears everything.</summary>
        public static byte Faction(ref SystemState state, Entity connection)
        {
            var entities = state.EntityManager;
            if (!entities.HasComponent<ConnectionPlayer>(connection))
            {
                return Simulation.Common.Faction.Neutral;
            }

            var player = entities.GetComponentData<ConnectionPlayer>(connection).Player;
            return entities.GetComponentData<Player>(player).Faction;
        }
    }
}
