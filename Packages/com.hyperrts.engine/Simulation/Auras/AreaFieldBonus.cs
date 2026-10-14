using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Auras
{
    /// <summary>A modifier the field grants, with the field id as its source.</summary>
    [InternalBufferCapacity(1)]
    public struct AreaFieldBonus : IBufferElementData
    {
        public StatModifier Modifier;
    }
}
