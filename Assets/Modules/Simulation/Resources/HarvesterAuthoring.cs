using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Lets a unit gather from resource nodes and carry cargo to a drop-off.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Harvester")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class HarvesterAuthoring : MonoBehaviour
    {
        [Tooltip("Cargo carried per trip.")]
        [Min(1)]
        public int capacity = 10;

        [Tooltip("Amount gathered per second while at a node.")]
        [Min(0.01f)]
        public float gatherRate = 2f;

        public class Baker : Baker<HarvesterAuthoring>
        {
            public override void Bake(HarvesterAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                HarvesterSetup.Add(ref sink, authoring.capacity, authoring.gatherRate);
            }
        }
    }

    public struct Harvester : IComponentData
    {
        public int Capacity;
        public float GatherRate;

        public UnityObjectRef<ResourceType> CargoType;
        public int CargoAmount;
    }
}
