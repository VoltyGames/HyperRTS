using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A mark the entity's shots leave where they land on open ground; one entry per <see cref="ImpactMark"/>.</summary>
    [InternalBufferCapacity(0)]
    public struct EntityImpactMark : IBufferElementData
    {
        public UnityObjectRef<ImpactMark> Mark;
    }
}
