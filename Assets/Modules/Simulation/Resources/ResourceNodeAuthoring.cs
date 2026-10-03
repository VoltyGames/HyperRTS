using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A neutral, harvestable deposit (supply pile, gold mine). Needs a collider to be right-clicked.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Node")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class ResourceNodeAuthoring : MonoBehaviour
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
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new ResourceNode
                {
                    Type = authoring.type,
                    Amount = authoring.amount,
                    MaxAmount = authoring.amount,
                    RegrowthPerSecond = authoring.regrowthPerSecond,
                });
                AddComponent(entity, new Faction { Value = Faction.Neutral });
            }
        }
    }

    public struct ResourceNode : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
        public int MaxAmount;
        public float RegrowthPerSecond;

        /// <summary>Fractional regrowth carried between frames.</summary>
        public float RegrowthAccumulator;
    }
}
