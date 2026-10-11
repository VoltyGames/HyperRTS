using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Swaps between child models as health drops (intact, damaged, wrecked), playing an effect at each step. The
    /// final collapse is the death spawn (a ruin with a Spawn Effect), not a stage.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Damage Stages")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Modules)]
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

            [Tooltip("Child object holding this stage's model.")]
            public GameObject visual;

            [Tooltip("Particle effect prefab played when health drops into this stage.")]
            public GameObject effect;
        }

        public class Baker : Baker<DamageStagesAuthoring>
        {
            public override void Bake(DamageStagesAuthoring authoring)
            {
                var stages = new List<DamageStage>();
                foreach (var stage in authoring.stages)
                {
                    if (stage.visual == null)
                    {
                        continue;
                    }

                    stages.Add(new DamageStage
                    {
                        Below = stage.below,
                        Visual = GetEntity(stage.visual, TransformUsageFlags.Dynamic),
                        Effect = stage.effect,
                    });
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.AddBuffer(stages);
                writer.Add(new ShownDamageStage { Index = -1 });
            }
        }
    }
}
