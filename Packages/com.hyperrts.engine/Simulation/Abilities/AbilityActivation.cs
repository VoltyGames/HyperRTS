using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// An ability used this frame, on the <see cref="AbilityEvents"/> singleton. Cleared at the start of each order
    /// phase; game systems read it after <see cref="AbilitySystem"/> to add their own effects.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AbilityActivation : IBufferElementData
    {
        public Entity Caster;
        public byte Faction;
        public int AbilityId;
        public Entity Target;
        public float3 Position;
    }

    /// <summary>Tags the singleton holding this frame's <see cref="AbilityActivation"/>s.</summary>
    public struct AbilityEvents : IComponentData { }
}
