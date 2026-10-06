using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Production data read from a prefab: seconds to build and population used.</summary>
    public struct Producible : IComponentData
    {
        /// <summary>Floor on build times used as divisors: zero means near-instant, never a division by zero.</summary>
        public const float MinBuildTime = 0.01f;

        public float BuildTime;
        public int Population;

        /// <summary>Seconds of work to construct or fully repair <paramref name="entity"/>, floored.</summary>
        public static float BuildTimeOf(in ComponentLookup<Producible> lookup, Entity entity, float floor = MinBuildTime) =>
            math.max(lookup.TryGetComponent(entity, out var producible) ? producible.BuildTime : 0f, floor);
    }
}
