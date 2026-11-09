using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// Swaps between child models as health drops (intact, damaged, wrecked), playing an effect at each step. The
    /// final collapse is the death spawn (a ruin with a Spawn Effect), not a stage.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Destruction/Damage Stages")]
    [Icon(HyperRTSIcons.DamageStagesAuthoring)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class DamageStagesAuthoring : AuthoringBehaviour
    {
        [Tooltip("From intact to most damaged. Each shows while health is below its threshold; the first should be 1.")]
        public List<Stage> stages = new();

        [Serializable]
        public class Stage
        {
            [Tooltip("Shown while health is at or below this fraction of the maximum.")]
            [Range(0f, 1f)]
            public float below = 1f;

            [Tooltip("Child object holding this stage's model. Leave empty for a stage that keeps the model and only plays its effect.")]
            public GameObject visual;

            [Tooltip("Particle effect prefab played when health drops into this stage.")]
            public GameObject effect;
        }

        public class Baker : Baker<DamageStagesAuthoring>
        {
            public override void Bake(DamageStagesAuthoring authoring)
            {
                if (authoring.stages.Count == 0)
                {
                    return;
                }

                // A stage without a model still counts: it plays its effect and raises battle smoke.
                var stages = new List<DamageStage>();
                foreach (var stage in authoring.stages)
                {
                    stages.Add(new DamageStage
                    {
                        Below = stage.below,
                        Visual = stage.visual != null ? GetEntity(stage.visual, TransformUsageFlags.Dynamic) : Entity.Null,
                        Effect = stage.effect,
                    });
                }

                var writer = this.Writer();
                writer.AddBuffer(stages);
                writer.Add(new ShownDamageStage { Index = -1 });
            }
        }
    }
}
