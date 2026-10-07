using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Common
{
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
}
