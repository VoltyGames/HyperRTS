using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Who a <see cref="PlayerCommand"/> addresses; callers apply their own ownership and capability filter.</summary>
    public static class CommandSubjects
    {
        /// <summary>
        /// The command's listed group, else its <see cref="PlayerCommand.Unit"/>, else every entity
        /// <paramref name="selected"/> matches.
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
