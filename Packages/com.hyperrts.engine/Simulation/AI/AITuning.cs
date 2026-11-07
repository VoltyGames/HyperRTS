using HyperRTS.Simulation.Common.Authoring;
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

        [Tooltip("Clear margin kept around each new building so units don't get boxed in.")]
        [Unit("m")]
        [Min(0f)]
        public float buildingGap = 4f;

        [Tooltip("Attack only enemies its team can see, or buildings it has explored, like a human; off knows every enemy position.")]
        public bool limitedByFog;

        [Tooltip("Multiplies the resources this AI's harvesters deliver. 1 is fair; above 1 is a labelled cheat.")]
        [Min(0.1f)]
        public float incomeMultiplier = 1f;

        [Tooltip("Squads that are pinned or hurt run for a retreat point.")]
        public bool retreatSquads = true;

        [Tooltip("A squad retreats when its living members fall to this share of its size.")]
        [Range(0f, 1f)]
        public float retreatLivingShare = 0.5f;

        [Tooltip("A squad retreats when its health falls to this share of its full health.")]
        [Range(0f, 1f)]
        public float retreatHealthShare = 0.33f;

        [Tooltip("Field work lines a builder squad digs at each defence point once the build order is done; 0 digs none.")]
        [Min(0)]
        public int fieldWorkLines = 1;

        [Tooltip("Also lays a barrier line, such as wire, in front of each defence point's cover.")]
        public bool fieldWorkBarrier;

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
            RetreatsSquads = retreatSquads,
            RetreatLivingShare = retreatLivingShare,
            RetreatHealthShare = retreatHealthShare,
            FieldWorkLines = fieldWorkLines,
            FieldWorkBarrier = fieldWorkBarrier,
        };
    }
}
