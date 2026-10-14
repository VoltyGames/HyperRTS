using HyperRTS.Simulation.Match;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Marks a computer-controlled player and tunes its skirmish behaviour.</summary>
    public struct AIPlayer : IComponentData
    {
        /// <summary>The preset it was tuned from, recorded in replays.</summary>
        public AIDifficulty Difficulty;

        public float ThinkInterval;
        public float TimeUntilThink;

        /// <summary>Idle combat units needed before the AI attacks.</summary>
        public int AttackWaveSize;

        public bool UseAbilities;

        /// <summary>Enemy distance that triggers self-targeted abilities.</summary>
        public float SelfAbilityTriggerRange;

        /// <summary>Clear margin kept around each building the AI places.</summary>
        public float BuildingGap;

        /// <summary>Attacks only what its team sees, or buildings it has explored; otherwise sweeps the map centre.</summary>
        public bool LimitedByFog;

        /// <summary>Round-robin cursor over production options.</summary>
        public int NextOption;

        /// <summary>
        /// Seed of the attack lane choice (see <see cref="AIRoutes.PickLane"/>). Bots differ by faction and vary by
        /// <see cref="Attacks"/> whatever it is; a game sets it from its match seed to vary matches too.
        /// </summary>
        public uint RouteSeed;

        /// <summary>Attacks sent along a lane so far.</summary>
        public int Attacks;
    }
}
