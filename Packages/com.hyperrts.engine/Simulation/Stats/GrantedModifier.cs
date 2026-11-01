using Unity.Entities;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>A modifier this entity grants to others, such as an aura's or a status effect's bonus.</summary>
    [InternalBufferCapacity(1)]
    public struct GrantedModifier : IBufferElementData
    {
        public StatModifier Modifier;
    }
}
