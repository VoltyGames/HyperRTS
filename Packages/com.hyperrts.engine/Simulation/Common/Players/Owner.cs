using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common.Players
{
    /// <summary>Owning player's owner number; 0 is neutral (resource nodes, civilians).</summary>
    public struct Owner : IComponentData
    {
        public const byte Neutral = 0;

        /// <summary>Highest player owner: <see cref="OwnerRelations"/> keeps a team for each in 30 bytes.</summary>
        public const byte Max = 29;

        [GhostField] public byte Value;

        /// <summary>The owner's bit in a 32-bit set of owners.</summary>
        public static uint Bit(byte owner) => 1u << owner;
    }
}
