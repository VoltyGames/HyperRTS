using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ground.Holes
{
    /// <summary>A building that cuts a hole in the drawn terrain once built; see <see cref="TerrainHoleAuthoring"/>.</summary>
    public struct TerrainHole : IComponentData
    {
        /// <summary>Hole centre in the building's local space, x and z metres.</summary>
        public float2 Center;

        /// <summary>Hole width and depth in metres.</summary>
        public float2 Size;
    }
}
