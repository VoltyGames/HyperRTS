using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Marks the singleton holding a map's bot hints, baked from <see cref="AIMapHintsAuthoring"/>, with buffers of
    /// <see cref="AISlotHint"/>, <see cref="AILaneHint"/> and <see cref="AIHintPoint"/>. Read it through
    /// <see cref="AIMapHintsView"/>.
    /// </summary>
    public struct AIMapHints : IComponentData
    {
    }

    /// <summary>One ground position (x, z) of the map's bot hints; slots and lanes hold start/count ranges of these.</summary>
    [InternalBufferCapacity(0)]
    public struct AIHintPoint : IBufferElementData
    {
        public float2 Position;
    }
}
