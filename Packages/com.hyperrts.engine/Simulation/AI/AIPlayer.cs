using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Marks a computer-controlled player and tunes its skirmish behaviour.</summary>
    public struct AIPlayer : IComponentData
    {
        public float ThinkInterval;
        public float TimeUntilThink;

        /// <summary>Idle combat units needed before the AI attacks.</summary>
        public int AttackWaveSize;

        public bool UseAbilities;

        /// <summary>Enemy distance that triggers self-targeted abilities.</summary>
        public float SelfCastRange;

        /// <summary>Clear margin kept around each building the AI places.</summary>
        public float BuildingGap;

        /// <summary>Attacks only what its team sees, or buildings it has explored; otherwise sweeps the map centre.</summary>
        public bool HonorsFog;

        /// <summary>Round-robin cursor over production options.</summary>
        public int NextOption;
    }
}
