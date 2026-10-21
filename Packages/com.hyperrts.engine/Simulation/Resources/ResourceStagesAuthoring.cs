using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// Swaps between child models as a resource node empties (a full pile, then smaller ones), and back up as a
    /// renewable node regrows.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Stages")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ResourceNodeAuthoring))]
    public class ResourceStagesAuthoring : AuthoringBehaviour
    {
        [Tooltip("From full to nearly spent. Each shows while the remaining share is at or below its threshold; the first should be 1.")]
        public List<Stage> stages = new();

        [Serializable]
        public class Stage
        {
            [Tooltip("Shown while the remaining amount is at or below this share of the starting amount.")]
            [Range(0f, 1f)]
            public float below = 1f;

            [Tooltip("Child object holding this stage's model.")]
            public GameObject visual;
        }

        public class Baker : Baker<ResourceStagesAuthoring>
        {
            public override void Bake(ResourceStagesAuthoring authoring)
            {
                var stages = new List<ResourceStage>();
                foreach (var stage in authoring.stages)
                {
                    stages.Add(new ResourceStage
                    {
                        Below = stage.below,
                        Visual = GetEntity(stage.visual, TransformUsageFlags.Dynamic),
                    });
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.AddBuffer(stages);
                writer.Add(new ShownResourceStage { Index = -1 });
            }
        }
    }
}
