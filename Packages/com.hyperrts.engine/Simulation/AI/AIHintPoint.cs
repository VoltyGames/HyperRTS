using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>One ground position (x, z) of the map's bot hints; slots and lanes hold start/count ranges of these.</summary>
    [InternalBufferCapacity(0)]
    public struct AIHintPoint : IBufferElementData
    {
        public float2 Position;
    }
}
