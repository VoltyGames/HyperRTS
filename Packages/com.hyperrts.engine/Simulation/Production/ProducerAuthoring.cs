using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Upgrades;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Lets a building train the listed units and research upgrades through one queue (barracks, lab).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Producer")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(BuildingAuthoring), "a Building")]
    public class ProducerAuthoring : AuthoringBehaviour
    {
        [Tooltip("Unit prefabs this building can train.")]
        public List<UnitAuthoring> productionOptions = new();

        [Tooltip("Upgrade prefabs this building can research, each once per player.")]
        public List<UpgradeAuthoring> researchOptions = new();

        [Tooltip("Local offset where finished units appear.")]
        public Vector3 spawnOffset = new(0f, 0f, -4f);

        [Tooltip("Maximum queued units.")]
        [Range(1, 10)]
        public int queueLimit = 5;

        public class Baker : Baker<ProducerAuthoring>
        {
            public override void Bake(ProducerAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                var options = ProducerSetup.Add(ref sink, authoring.spawnOffset, authoring.queueLimit);
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

    /// <summary>Trains the units in its <see cref="ProductionQueueItem"/> queue, one at a time.</summary>
    public struct Producer : IComponentData
    {
        public float3 SpawnOffset;
        public int QueueLimit;

        /// <summary>Production speed multiplier (1 = build times as listed) from stats; low power slows it further.</summary>
        public float Speed;

        /// <summary>Seconds of work done on the head of the queue.</summary>
        [GhostField] public float Elapsed;
    }
}
