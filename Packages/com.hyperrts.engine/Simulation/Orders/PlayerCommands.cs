using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Reading <see cref="PlayerCommand"/> buffers: a cheap pre-check and who each command addresses.</summary>
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

        /// <summary>Players that can hold commands; build it in OnCreate for <see cref="Any"/>.</summary>
        public static EntityQuery Query(ref SystemState state) =>
            new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerCommand>().Build(ref state);

        /// <summary>
        /// Whether any player recorded a command whose type is in <paramref name="mask"/> this frame, so command
        /// systems skip their job sync on the many frames without commands.
        /// </summary>
        public static bool Any(ref SystemState state, EntityQuery players, ulong mask)
        {
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

        /// <summary>
        /// Who a command addresses (callers apply their own ownership filter): the command's listed group, else its
        /// <see cref="PlayerCommand.Unit"/>, else every entity <paramref name="selected"/> matches.
        /// </summary>
        public static void Collect(in PlayerCommand command, DynamicBuffer<PlayerCommandSubject> listed,
            EntityQuery selected, NativeList<Entity> subjects)
        {
            subjects.Clear();
            if (command.SubjectCount > 0)
            {
                for (var i = 0; i < command.SubjectCount; i++)
                {
                    subjects.Add(listed[command.SubjectStart + i].Value);
                }

                return;
            }

            if (command.Unit != Entity.Null)
            {
                subjects.Add(command.Unit);
                return;
            }

            subjects.AddRange(selected.ToEntityArray(Allocator.Temp));
        }

        public static NativeList<Entity> Collect(in PlayerCommand command, DynamicBuffer<PlayerCommandSubject> listed,
            EntityQuery selected)
        {
            var subjects = new NativeList<Entity>(Allocator.Temp);
            Collect(command, listed, selected, subjects);
            return subjects;
        }
    }
}
