using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the refill an owned entity gives its owner's resource reserves.</summary>
    public static class ReserveRefillSetup
    {
        public static void Add<TWriter>(ref TWriter writer, UnityObjectRef<ResourceType> type, int amount,
            float interval) where TWriter : struct, IEntityWriter =>
            writer.Add(new ReserveRefill { Type = type, Amount = amount, Interval = interval });
    }
}
