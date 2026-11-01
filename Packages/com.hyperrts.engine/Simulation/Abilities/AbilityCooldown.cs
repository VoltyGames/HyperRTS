using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>The running cooldown of the <see cref="Ability"/> with the same id; the only ability data that replicates.</summary>
    [InternalBufferCapacity(0)]
    public struct AbilityCooldown : IBufferElementData
    {
        [GhostField] public int Id;

        /// <summary>Replicated to a tenth of a second, enough for the HUD.</summary>
        [GhostField(Quantization = 10)] public float Remaining;

        public readonly bool IsReady => Remaining <= 0f;
    }
}
