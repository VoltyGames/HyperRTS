using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Singleton on the <see cref="FogOfWar"/> grid: how far each cell's ground rises above the mean ground around it,
    /// and what that is worth under the match's <see cref="HighGroundRules"/>.
    /// </summary>
    public struct HighGround : IComponentData
    {
        /// <summary>Metres each cell's ground stands above the mean within <see cref="HighGroundRules.Radius"/>.</summary>
        public NativeArray<float> Rise;

        public int2 Size;
        public float2 Min;
        public float CellSize;
        public HighGroundRules Rules;

        public readonly bool IsCreated => Rise.IsCreated;

        public readonly float RiseAt(float3 position)
        {
            var cell = (int2)math.floor((position.xz - Min) / CellSize);
            return math.all(cell >= 0 & cell < Size) ? Rise[cell.y * Size.x + cell.x] : 0f;
        }

        /// <summary>What a viewer's sight range is multiplied by there; 1 off high ground or without a grid.</summary>
        public readonly float SightScale(float3 position) =>
            IsCreated ? 1f + math.clamp(RiseAt(position) * Rules.SightPerMetre, 0f, Rules.MaxSight) : 1f;
    }

    /// <summary>On the match: what standing higher is worth to sight and to direct fire.</summary>
    public struct HighGroundRules : IComponentData
    {
        /// <summary>High ground is measured against the mean ground within this many metres.</summary>
        public float Radius;

        public float SightPerMetre;
        public float MaxSight;
        public float RangePerMetre;
        public float MaxRange;

        /// <summary>Share of range a direct-fire shooter gains standing <paramref name="above"/> metres over its target.</summary>
        public readonly float RangeBonus(float above) => math.clamp(above * RangePerMetre, 0f, MaxRange);
    }
}
