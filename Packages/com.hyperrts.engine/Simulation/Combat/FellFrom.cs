using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Where a <see cref="Topple"/> piece's lethal hit came from.</summary>
    public struct FellFrom : IComponentData
    {
        public float3 Position;
    }
}
