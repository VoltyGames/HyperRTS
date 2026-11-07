using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Lets a building train the listed units and research upgrades through one queue (barracks, lab).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Producer")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Production)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class ProducerAuthoring : AuthoringBehaviour
    {
        [Tooltip("Unit prefabs this building can train.")]
        [ProducibleOption, PrefabAsset("Production option"), NoEmptyEntries]
        public List<UnitAuthoring> productionOptions = new();

        [Tooltip("Research prefabs (upgrades) this building can research, each once per player.")]
        [ProducibleOption, PrefabAsset("Research option"), NoEmptyEntries]
        public List<UpgradeAuthoring> researchOptions = new();

        [Tooltip("Local offset where finished units appear.")]
        public Vector3 spawnOffset = new(0f, 0f, -4f);

        [Tooltip("Maximum queued units.")]
        [Range(1, 10)]
        public int queueLimit = 5;

        public static DynamicBuffer<ProductionOption> Add(EntityWriter writer, float3 spawnOffset,
            int queueLimit)
        {
            writer.Add(new Producer { SpawnOffset = spawnOffset, QueueLimit = queueLimit, Speed = 1f });
            writer.AddBuffer<ProductionQueueItem>();
            writer.Add<RallyPoint>();
            writer.SetEnabled<RallyPoint>(false);
            return writer.AddBuffer<ProductionOption>();
        }

        public class Baker : Baker<ProducerAuthoring>
        {
            public override void Bake(ProducerAuthoring authoring)
            {
                var writer = this.Writer();
                var options = ProducerAuthoring.Add(writer, authoring.spawnOffset, authoring.queueLimit);
                AddOptions(options, authoring.productionOptions, TransformUsageFlags.Dynamic);
                AddOptions(options, authoring.researchOptions, TransformUsageFlags.None);
            }

            private void AddOptions<T>(DynamicBuffer<ProductionOption> options, List<T> prefabs,
                TransformUsageFlags flags) where T : Component
            {
                foreach (var prefab in prefabs)
                {
                    if (prefab != null)
                    {
                        options.Add(new ProductionOption { Prefab = GetEntity(prefab, flags) });
                    }
                }
            }
        }
    }
}
