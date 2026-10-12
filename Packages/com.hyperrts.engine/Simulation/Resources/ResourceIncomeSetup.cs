using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the passive income an owned entity pays its owner, and its optional per-occupant rate and reserve.</summary>
    public static class ResourceIncomeSetup
    {
        public static void Add<TWriter>(ref TWriter writer, UnityObjectRef<ResourceType> type, int amount,
            float interval, int firstOwnerBonus) where TWriter : struct, IEntityWriter =>
            writer.Add(new ResourceIncome
            {
                Type = type,
                Amount = amount,
                Interval = interval,
                FirstOwnerBonus = firstOwnerBonus,
            });

        public static void AddPerOccupant<TWriter>(ref TWriter writer) where TWriter : struct, IEntityWriter =>
            writer.Add<OccupantIncome>();

        /// <summary>Adds a full reserve of <paramref name="capacity"/>.</summary>
        public static void AddReserve<TWriter>(ref TWriter writer, int capacity) where TWriter : struct, IEntityWriter =>
            writer.Add(new ResourceReserve { Remaining = capacity, Capacity = capacity });
    }
}
