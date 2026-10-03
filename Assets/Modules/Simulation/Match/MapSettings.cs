using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Playable area on the XZ plane plus the grid resolutions derived from it.</summary>
    public struct MapSettings : IComponentData
    {
        public float2 Min;
        public float2 Size;
        public float NavCellSize;
        public float FogCellSize;
        public bool FogOfWar;

        public readonly float2 Max => Min + Size;

        public readonly bool Contains(float3 position) =>
            math.all(position.xz >= Min) && math.all(position.xz <= Max);

        public readonly float3 Clamp(float3 position) =>
            new(math.clamp(position.x, Min.x, Max.x), position.y, math.clamp(position.z, Min.y, Max.y));
    }
}
