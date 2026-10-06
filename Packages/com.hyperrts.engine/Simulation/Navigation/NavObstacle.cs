using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Axis-aligned XZ box that pathing and building placement treat as solid.</summary>
    public struct NavObstacle : IComponentData
    {
        public float2 Size;
    }
}
