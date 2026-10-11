using HyperRTS.Simulation.Match;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Baked tuning of every difficulty on the match entity, so a slot can become AI at runtime.</summary>
    public struct AIDifficultyTuning : IBufferElementData
    {
        public AIDifficulty Difficulty;
        public AIPlayer Tuning;

        /// <summary>Multiplies resources this AI delivers; 1 is fair.</summary>
        public float IncomeMultiplier;

        /// <summary>Whether <paramref name="incomeMultiplier"/> is 1 within float noise, so no bonus is needed.</summary>
        public static bool IsFairIncome(float incomeMultiplier) => math.abs(incomeMultiplier - 1f) <= 0.001f;
    }
}
