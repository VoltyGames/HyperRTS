using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Identity of a player entity; its team comes from <see cref="FactionRelations.TeamOf"/>.</summary>
    public struct Player : IComponentData
    {
        [GhostField] public byte Faction;
        [GhostField] public FixedString32Bytes Name;

        /// <summary>Linear RGBA team colour.</summary>
        [GhostField] public float4 Color;
    }
}
