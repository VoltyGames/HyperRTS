using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Marks the singleton that holds a map's bot hints, baked from <see cref="AIMapHintsAuthoring"/>. Its buffers:
    /// <see cref="AISlotHint"/> (per slot: expansion order and defence points), <see cref="AILaneHint"/> (attack
    /// lanes between enemy slots) and <see cref="AIHintPoint"/> (the positions both of them point into).
    /// Read it through <see cref="AIMapHintsView"/>.
    /// </summary>
    public struct AIMapHints : IComponentData
    {
    }
}
