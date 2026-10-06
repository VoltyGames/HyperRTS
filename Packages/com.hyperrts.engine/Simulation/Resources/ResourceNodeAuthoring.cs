using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A neutral, harvestable deposit (supply pile, gold mine). Needs a collider to be right-clicked.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Node")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class ResourceNodeAuthoring : AuthoringBehaviour
    {
        [Tooltip("Resource this node yields.")]
        public ResourceType type;

        [Tooltip("Starting and maximum amount.")]
        [Min(1)]
        public int amount = 1000;

        [Tooltip("Amount restored per second; 0 means the node is removed when depleted.")]
        [Min(0f)]
        public float regrowthPerSecond;

        public class Baker : Baker<ResourceNodeAuthoring>
        {
            public override void Bake(ResourceNodeAuthoring authoring)
            {
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.Dynamic));
                ResourceNodeSetup.Add(ref sink, authoring.type, authoring.amount, authoring.regrowthPerSecond);
            }
        }
    }

    /// <summary>Remaining amount of a harvestable deposit and its regrowth.</summary>
    public struct ResourceNode : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        [GhostField] public int Amount;
        [GhostField] public int MaxAmount;
        public float RegrowthPerSecond;

        /// <summary>Fractional regrowth carried between frames.</summary>
        public float RegrowthAccumulator;
    }
}
