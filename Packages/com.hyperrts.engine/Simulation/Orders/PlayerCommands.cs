using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Cheap pre-check so command systems skip their job sync on the many frames without commands.</summary>
    public static class PlayerCommands
    {
        /// <summary>A set of built-in command types for <see cref="Any"/>; custom types are never matched.</summary>
        public static ulong Mask(CommandType type) => (int)type < 64 ? 1ul << (int)type : 0;

        public static ulong Mask(CommandType first, CommandType last)
        {
            var mask = 0ul;
            for (var type = (int)first; type <= (int)last; type++)
            {
                mask |= Mask((CommandType)type);
            }

            return mask;
        }

        /// <summary>Whether any player recorded a command whose type is in <paramref name="mask"/> this frame.</summary>
        public static bool Any(ref SystemState state, ulong mask)
        {
            var players = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerCommand>().Build(ref state);
            foreach (var player in players.ToEntityArray(Allocator.Temp))
            {
                foreach (var command in state.EntityManager.GetBuffer<PlayerCommand>(player, true))
                {
                    if ((mask & Mask(command.Type)) != 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
