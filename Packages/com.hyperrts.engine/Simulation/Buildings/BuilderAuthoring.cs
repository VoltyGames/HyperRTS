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
                foreach (var option in authoring.buildOptions)
                {
                    if (option != null)
                    {
                        options.Add(new BuildOption { Prefab = GetEntity(option, TransformUsageFlags.Dynamic) });
                    }
                }
            }
        }
    }
}
