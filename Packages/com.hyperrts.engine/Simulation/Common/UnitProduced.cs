using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Common
{
    /// <summary>A producer finished a unit this frame; the unit itself appears when the frame's commands play back.</summary>
    [InternalBufferCapacity(0)]
    public struct UnitProduced : IBufferElementData
    {
        public Entity Producer;
        public Entity Prefab;
        public byte Faction;
        public float3 Position;
    }
}
