using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Identity of a player entity; its team comes from <see cref="FactionRelations.TeamOf"/>.</summary>
    public struct Player : IComponentData
    {
        public byte Faction;
        public FixedString32Bytes Name;

        /// <summary>Linear RGBA team colour.</summary>
        public float4 Color;
    }
}
