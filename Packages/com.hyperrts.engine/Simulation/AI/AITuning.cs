using System;
using HyperRTS.Simulation.Match;
using UnityEngine;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Behaviour knobs of one <see cref="AIDifficulty"/>, baked into the player's <see cref="AIPlayer"/>.</summary>
    [Serializable]
    public class AITuning
    {
        [Tooltip("Seconds between AI decisions.")]
        [Min(0.1f)]
        public float thinkInterval = 2f;

        [Tooltip("Idle combat units the AI gathers before attacking.")]
        [Min(1)]
        public int attackWaveSize = 6;

        [Tooltip("Fire ready unit, building and player abilities.")]
        public bool useAbilities = true;

        [Tooltip("How close an enemy must come before self-targeted abilities (smoke, self-heal) are fired.")]
        [Min(0f)]
        public float selfAbilityTriggerRange = 10f;

        [Tooltip("Clear margin (metres) kept around each new building so units don't get boxed in.")]
        [Min(0f)]
        public float buildingGap = 4f;

        [Tooltip("Attack only enemies its team can see, or buildings it has explored, like a human; off knows every enemy position.")]
        public bool limitedByFog;

        [Tooltip("Multiplies the resources this AI's harvesters deliver. 1 is fair; above 1 is a labelled cheat.")]
        [Min(0.1f)]
        public float incomeMultiplier = 1f;

        public AIDifficultyTuning ToTuning(AIDifficulty difficulty) => new()
        {
            Difficulty = difficulty,
            Tuning = ToComponent(difficulty),
            IncomeMultiplier = incomeMultiplier,
        };

        public AIPlayer ToComponent(AIDifficulty difficulty) => new()
        {
            Difficulty = difficulty,
            ThinkInterval = thinkInterval,
            TimeUntilThink = thinkInterval,
            AttackWaveSize = attackWaveSize,
            UseAbilities = useAbilities,
            SelfAbilityTriggerRange = selfAbilityTriggerRange,
            BuildingGap = buildingGap,
            LimitedByFog = limitedByFog,
        };
    }
}
