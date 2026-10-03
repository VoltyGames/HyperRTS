using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Lets a building train the listed units through a queue (barracks, factory).</summary>
    [AddComponentMenu(HyperRTSMenu.Buildings + "Producer")]
    [Icon(HyperRTSIcons.Buildings)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class ProducerAuthoring : MonoBehaviour
    {
        [Tooltip("Unit prefabs this building can train.")]
        public List<UnitAuthoring> productionOptions = new();

        [Tooltip("Local offset where finished units appear.")]
        public Vector3 spawnOffset = new(0f, 0f, -4f);

        [Tooltip("Maximum queued units.")]
        [Range(1, 10)]
        public int queueLimit = 5;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.TransformPoint(spawnOffset), 0.5f);
        }

        public class Baker : Baker<ProducerAuthoring>
        {
            public override void Bake(ProducerAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                var options = ProducerSetup.Add(ref sink, authoring.spawnOffset, authoring.queueLimit);
                foreach (var option in authoring.productionOptions)
                {
                    if (option != null)
                    {
                        options.Add(new ProductionOption { Prefab = GetEntity(option, TransformUsageFlags.Dynamic) });
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

        /// <summary>Seconds spent on the head of the queue.</summary>
        public float Elapsed;
    }

    /// <summary>A unit prefab a producer can train.</summary>
    [InternalBufferCapacity(6)]
    public struct ProductionOption : IBufferElementData
    {
        public Entity Prefab;
    }

    /// <summary>Paid-for units waiting to be trained; the first element is in progress.</summary>
    [InternalBufferCapacity(5)]
    public struct ProductionQueueItem : IBufferElementData
    {
        public Entity Prefab;
    }

    /// <summary>Where new units walk after spawning; disabled means stay at the spawn point.</summary>
    public struct RallyPoint : IComponentData, IEnableableComponent
    {
        public float3 Position;
    }
}
