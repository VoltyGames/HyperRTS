using HyperRTS.Simulation.Common;
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
        public static float GetBuildTime(in ComponentLookup<Producible> lookup, Entity entity, float floor = MinBuildTime) =>
            math.max(lookup.TryGetComponent(entity, out var producible) ? producible.BuildTime : 0f, floor);
    }

    /// <summary>Most of this type one player may have at once, counting standing, unfinished and queued ones.</summary>
    public struct BuildLimit : IComponentData
    {
        public int Max;
    }

    /// <summary>
    /// A completed building or finished research of this <see cref="EntityInfo.TypeId"/> the owner must have before
    /// producing.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct Prerequisite : IBufferElementData
    {
        public int TypeId;
    }

    /// <summary>
    /// Marks a producible that is researched rather than built: finishing it records a <see cref="CompletedResearch"/>
    /// on the owner instead of spawning anything. Each is researched once per player.
    /// </summary>
    public struct Research : IComponentData { }
}
