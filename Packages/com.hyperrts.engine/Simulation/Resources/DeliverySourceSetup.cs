using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Adds the carrier deliveries an owned entity receives.</summary>
    public static class DeliverySourceSetup
    {
        public static void Add<TWriter>(ref TWriter writer, Entity carrier, int count, float interval,
            UnityObjectRef<ResourceType> type, int amount, float arrivalRange, float spacing)
            where TWriter : struct, IEntityWriter =>
            writer.Add(new DeliverySource
            {
                Carrier = carrier,
                Count = count,
                Interval = interval,
                Type = type,
                Amount = amount,
                ArrivalRange = arrivalRange,
                Spacing = spacing,
            });
    }
}
