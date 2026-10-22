using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Owning player's faction number; 0 is neutral (resource nodes, civilians).</summary>
    public struct Faction : IComponentData
    {
        public const byte Neutral = 0;

        /// <summary>Highest player faction: <see cref="FactionRelations"/> keeps a team for each in 30 bytes.</summary>
        public const byte Max = 29;

        [GhostField] public byte Value;

        /// <summary>The faction's bit in a 32-bit set of factions.</summary>
        public static uint Bit(byte faction) => 1u << faction;
    }
}
