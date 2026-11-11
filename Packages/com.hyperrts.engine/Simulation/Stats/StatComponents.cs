using System;
using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>A stat's unmodified value, recorded from the authored component when the entity spawns.</summary>
    [InternalBufferCapacity(0)]
    public struct BaseStat : IBufferElementData
    {
        public Stat Stat;
        public float Value;

        /// <summary>The stat's base value, recorded when the entity spawned.</summary>
        public static float Read(DynamicBuffer<BaseStat> bases, Stat stat)
        {
            foreach (var item in bases)
            {
                if (item.Stat == stat)
                {
                    return item.Value;
                }
            }

            throw new InvalidOperationException("The entity has no recorded base for this stat.");
        }
    }

    /// <summary>Enabled on a fresh entity until its <see cref="BaseStat"/>s are recorded from the authored values.</summary>
    public struct StatBasesPending : IComponentData, IEnableableComponent
    {
    }
}
