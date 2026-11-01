using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Singleton on the <see cref="FogOfWar"/> grid: how far each cell's ground rises above the mean ground around it,
    /// and what that is worth under the match's <see cref="SightRules"/>.
    /// </summary>
    public struct HighGround : IComponentData
    {
        /// <summary>Metres each cell's ground stands above the mean within <see cref="SightRules.Radius"/>.</summary>
        public NativeArray<float> Rise;

        public int2 Size;
        public float2 Min;
        public float CellSize;
        public SightRules Rules;

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
}
