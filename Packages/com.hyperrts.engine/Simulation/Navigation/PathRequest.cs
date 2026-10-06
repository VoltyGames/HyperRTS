using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>One pending path search, snapshotted so the search job needs no component reads.</summary>
    internal struct PathRequest
    {
        public Entity Entity;
        public float3 Start;
        public float3 Goal;
        public float Radius;
        public NavLayer Layer;
        public uint Frame;
    }
}
