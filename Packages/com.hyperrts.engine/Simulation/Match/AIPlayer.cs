using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Marks a computer-controlled player and tunes its skirmish behaviour.</summary>
    public struct AIPlayer : IComponentData
    {
        public float ThinkInterval;
        public float TimeUntilThink;

        /// <summary>Idle combat units needed before the AI attacks.</summary>
        public int AttackWaveSize;

        public bool UseAbilities;

        /// <summary>Round-robin cursor over production options.</summary>
        public int NextOption;
    }
}
