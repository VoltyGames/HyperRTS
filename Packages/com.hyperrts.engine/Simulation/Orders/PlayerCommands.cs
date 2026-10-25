using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Who a <see cref="PlayerCommand"/> addresses; command systems read through <see cref="CommandReader"/>.</summary>
    public static class PlayerCommands
    {
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

        /// <summary>
        /// Lists <paramref name="units"/> as <paramref name="command"/>'s own group, so its moves share one formation
        /// and flow field. Later commands may copy the command to address the same group.
        /// </summary>
        public static void Address(ref PlayerCommand command, DynamicBuffer<PlayerCommandSubject> listed,
            NativeArray<Entity> units)
        {
            command.Unit = Entity.Null;
            command.SubjectStart = (ushort)listed.Length;
            command.SubjectCount = (ushort)units.Length;
            foreach (var unit in units)
            {
                listed.Add(new PlayerCommandSubject { Value = unit });
            }
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
