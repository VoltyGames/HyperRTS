using HyperRTS.Simulation.Common;
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

        /// <summary>
        /// The difficulty income modifier for <paramref name="incomeMultiplier"/>; false when it is 1 within float noise,
        /// so no bonus is needed.
        /// </summary>
        public static bool TryGetIncomeBonus(float incomeMultiplier, out StatModifier bonus)
        {
            bonus = new StatModifier
            {
                Stat = Stat.Income, Percent = incomeMultiplier - 1f, Source = StatSource.Difficulty,
            };
            return math.abs(bonus.Percent) > 0.001f;
        }
    }
}
