using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Who a <see cref="PlayerCommand"/> addresses; callers apply their own ownership and capability filter.</summary>
    public static class CommandSubjects
    {
        /// <summary>The commanded unit, else every entity <paramref name="selected"/> matches.</summary>
        public static void Collect(Entity unit, EntityQuery selected, NativeList<Entity> subjects)
        {
            subjects.Clear();
            if (unit != Entity.Null)
            {
                subjects.Add(unit);
            }
            else
            {
                subjects.AddRange(selected.ToEntityArray(Allocator.Temp));
            }
        }

        public static NativeList<Entity> Collect(Entity unit, EntityQuery selected)
        {
            var subjects = new NativeList<Entity>(Allocator.Temp);
            Collect(unit, selected, subjects);
            return subjects;
        }
    }
}
