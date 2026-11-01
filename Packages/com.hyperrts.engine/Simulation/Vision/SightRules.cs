using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>On the match: what standing higher is worth to sight and direct fire, and how close spots concealment.</summary>
    public struct SightRules : IComponentData
    {
        /// <summary>High ground is measured against the mean ground within this many metres.</summary>
        public float Radius;

        public float SightPerMetre;
        public float MaxSight;
        public float RangePerMetre;
        public float MaxRange;

        /// <summary>Concealed infantry show to enemy ground units this close, in metres.</summary>
        public float RevealDistance;

        /// <summary>Share of range a direct-fire shooter gains standing <paramref name="above"/> metres over its target.</summary>
        public readonly float RangeBonus(float above) => math.clamp(above * RangePerMetre, 0f, MaxRange);
    }
}
