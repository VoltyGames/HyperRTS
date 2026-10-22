using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Identity of a player entity; its team comes from <see cref="FactionRelations.GetTeam"/>.</summary>
    public struct Player : IComponentData
    {
        [GhostField] public byte Faction;
        [GhostField] public FixedString32Bytes Name;

        /// <summary>Linear RGBA team colour.</summary>
        [GhostField] public float4 Color;
    }

    /// <summary>The game's army or nation id for a player (0 = none); the engine never interprets it.</summary>
    public struct PlayerSide : IComponentData
    {
        [GhostField] public byte Value;
    }

    /// <summary>A player's running match totals, counted by the authoritative world for the end-of-match summary.</summary>
    public struct PlayerStats : IComponentData
    {
        [GhostField] public int UnitsBuilt;
        [GhostField] public int UnitsLost;
        [GhostField] public int UnitsKilled;
        [GhostField] public int BuildingsBuilt;
        [GhostField] public int BuildingsLost;
        [GhostField] public int BuildingsDestroyed;

        /// <summary>Every resource type delivered to a drop-off, after income multipliers.</summary>
        [GhostField] public int ResourcesGathered;
    }

    /// <summary>The player this client controls; input and HUD read it.</summary>
    public struct LocalPlayer : IComponentData { }

    /// <summary>Enabled on a player once it owns no <c>VictoryCritical</c> entity.</summary>
    [GhostEnabledBit]
    public struct Defeated : IComponentData, IEnableableComponent { }
}
