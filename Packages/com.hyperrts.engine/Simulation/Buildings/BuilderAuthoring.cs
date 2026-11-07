using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Lets a unit place and construct the listed buildings (worker, dozer).</summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Buildings/Builder")]
    [Icon(HyperRTSIcons.BuilderAuthoring)]
    [HelpURL(HyperRTSDocs.Buildings)]
    [DisallowMultipleComponent]
    public class BuilderAuthoring : AuthoringBehaviour
    {
        [Tooltip("Building prefabs this unit can place.")]
        [PrefabAsset("Build option"), NoEmptyEntries]
        public List<BuildingAuthoring> buildOptions = new();

        [Tooltip("Construction speed multiplier; several builders on one site add up.")]
        [Min(0.01f)]
        public float buildRate = 1f;

        public static DynamicBuffer<BuildOption> Add(EntityWriter writer, float rate)
        {
            writer.Add(new Builder { Rate = rate });
            return writer.AddBuffer<BuildOption>();
        }

        public class Baker : Baker<BuilderAuthoring>
        {
            public override void Bake(BuilderAuthoring authoring)
            {
                var writer = this.Writer();
                var options = BuilderAuthoring.Add(writer, authoring.buildRate);
                foreach (var building in authoring.buildOptions)
                {
                    if (building != null)
                    {
                        options.Add(new BuildOption { Prefab = GetEntity(building, TransformUsageFlags.Dynamic) });
                    }
                }
            }
        }
    }
}
