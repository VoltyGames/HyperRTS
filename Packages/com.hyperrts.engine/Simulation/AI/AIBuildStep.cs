using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>One build-order step on an AI player: own <see cref="Count"/> of a building, unit or upgrade prefab.</summary>
    [InternalBufferCapacity(0)]
    public struct AIBuildStep : IBufferElementData
    {
        public Entity Prefab;
        public int Count;
    }
}
