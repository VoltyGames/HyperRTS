using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Lets a unit place and construct the listed buildings (worker, dozer).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Builder")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class BuilderAuthoring : MonoBehaviour
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
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Builder { Rate = authoring.buildRate });

                var options = AddBuffer<BuildOption>(entity);
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

    public struct Builder : IComponentData
    {
        public float Rate;
    }

    /// <summary>A building prefab a builder can place.</summary>
    [InternalBufferCapacity(4)]
    public struct BuildOption : IBufferElementData
    {
        public Entity Prefab;
    }
}
