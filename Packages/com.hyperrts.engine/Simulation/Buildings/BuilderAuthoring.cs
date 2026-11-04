using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Core;
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
        [PrefabAsset("Build option"), NoEmptyEntries]
        public List<BuildingAuthoring> buildOptions = new();

        [Tooltip("Letters that pick the build options in order, one each, e.g. BCV; fewer than the options leaves the rest without a key.")]
        public string hotkeys = "";

        [Tooltip("Building prefabs on the command card's second page, behind a More card.")]
        [PrefabAsset("Advanced build option"), NoEmptyEntries]
        public List<BuildingAuthoring> advancedBuildOptions = new();

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
                AddOptions(options, authoring.buildOptions, false, authoring.hotkeys);
                AddOptions(options, authoring.advancedBuildOptions, true, "");
            }

            private void AddOptions(DynamicBuffer<BuildOption> options, List<BuildingAuthoring> buildings,
                bool advanced, string hotkeys)
            {
                for (var i = 0; i < buildings.Count; i++)
                {
                    var building = buildings[i];
                    if (building != null)
                    {
                        options.Add(new BuildOption
                        {
                            Prefab = GetEntity(building, TransformUsageFlags.Dynamic), Advanced = advanced,
                            Hotkey = i < hotkeys.Length ? char.ToUpperInvariant(hotkeys[i]) : '\0',
                        });
                    }
                }
            }
        }
    }
}
