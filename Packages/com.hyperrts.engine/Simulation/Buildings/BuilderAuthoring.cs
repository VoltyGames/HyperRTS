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
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class BuilderAuthoring : AuthoringBehaviour
    {
        [Tooltip("Building prefabs this unit can place.")]
        public List<BuildingAuthoring> buildOptions = new();

        [Tooltip("Construction speed multiplier; several builders on one site add up.")]
        [Min(0.01f)]
        public float buildRate = 1f;

        public class Baker : Baker<BuilderAuthoring>
        {
            public override void Bake(BuilderAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var options = BuilderSetup.Add(ref writer, authoring.buildRate);
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
