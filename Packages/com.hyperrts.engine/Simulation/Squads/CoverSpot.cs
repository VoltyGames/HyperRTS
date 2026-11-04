using HyperRTS.Simulation.Common.Infantry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// A place on a prop or field work where a soldier takes cover, in the entity's local space. The buffer goes with
    /// the entity, so a destroyed prop loses its spots.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CoverSpot : IBufferElementData
    {
        public float3 Position;

        /// <summary>Direction on the XZ plane a soldier in the spot faces, toward the side the fire comes from.</summary>
        public float2 Facing;

        public CoverKind Kind;

        public readonly ulong Hash() => math.hash(new float4(Position, Facing.x + Facing.y * 7f + (float)Kind * 13f));
    }
}
