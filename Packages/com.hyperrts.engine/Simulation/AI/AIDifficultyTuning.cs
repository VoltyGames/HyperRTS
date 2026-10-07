using HyperRTS.Simulation.Match;
using Unity.Entities;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Baked tuning of every difficulty on the match entity, so a slot can become AI at runtime.</summary>
    public struct AIDifficultyTuning : IBufferElementData
    {
        public AIDifficulty Difficulty;
        public AIPlayer Tuning;

        /// <summary>Multiplies resources this AI delivers; 1 is fair.</summary>
        public float IncomeMultiplier;
    }
}
