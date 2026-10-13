using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>On a <see cref="Topple"/> piece: where its lethal hit came from (shooter, crusher or blast centre).</summary>
    public struct FellFrom : IComponentData
    {
        public float3 Position;
    }
}
