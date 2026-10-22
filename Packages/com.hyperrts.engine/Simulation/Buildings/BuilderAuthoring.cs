using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Lets a unit place and construct the listed buildings (worker, dozer).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Builder")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Buildings)]
    [DisallowMultipleComponent]
    public class BuilderAuthoring : AuthoringBehaviour
    {
        [Tooltip("Building prefabs this unit can place.")]
        public List<BuildingAuthoring> buildOptions = new();

        [Tooltip("Building prefabs on the command card's second page, behind a More card.")]
        public List<BuildingAuthoring> advancedBuildOptions = new();

        [Tooltip("Construction speed multiplier; several builders on one site add up.")]
        [Min(0.01f)]
        public float buildRate = 1f;

        public static DynamicBuffer<BuildOption> Add<TWriter>(ref TWriter writer, float rate)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Builder { Rate = rate });
            return writer.AddBuffer<BuildOption>();
        }

        public class Baker : Baker<BuilderAuthoring>
        {
            public override void Bake(BuilderAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var options = BuilderAuthoring.Add(ref writer, authoring.buildRate);
                AddOptions(options, authoring.buildOptions, false);
                AddOptions(options, authoring.advancedBuildOptions, true);
            }

            private void AddOptions(DynamicBuffer<BuildOption> options, List<BuildingAuthoring> buildings,
                bool advanced)
            {
                foreach (var building in buildings)
                {
                    if (building != null)
                    {
                        options.Add(new BuildOption
                        {
                            Prefab = GetEntity(building, TransformUsageFlags.Dynamic), Advanced = advanced,
                        });
                    }
                }
            }
        }
    }
}
