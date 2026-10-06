using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Axis-aligned XZ box stamped over the grid's terrain surfaces, before <see cref="NavObstacle"/>s.</summary>
    public struct NavArea : IComponentData
    {
        public float2 Size;
        public NavAreaKind Kind;
    }
}
