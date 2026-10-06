using System;
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
        public float selfCastRange = 10f;

        [Tooltip("Clear margin (metres) kept around each new building so units don't get boxed in.")]
        [Min(0f)]
        public float buildingGap = 2f;

        public AIPlayer ToComponent() => new()
        {
            ThinkInterval = thinkInterval,
            TimeUntilThink = thinkInterval,
            AttackWaveSize = attackWaveSize,
            UseAbilities = useAbilities,
            SelfCastRange = selfCastRange,
            BuildingGap = buildingGap,
        };
    }
}
