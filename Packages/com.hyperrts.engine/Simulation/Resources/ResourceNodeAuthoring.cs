using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>A neutral, harvestable deposit (supply pile, gold mine). Needs a collider to be right-clicked.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Resource Node")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    [RequiresGhost]
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

        public static void Add<TWriter>(ref TWriter writer, UnityObjectRef<ResourceType> type, int amount,
            float regrowthPerSecond) where TWriter : struct, IEntityWriter
        {
            writer.Add(new ResourceNode
            {
                Type = type,
                Amount = amount,
                MaxAmount = amount,
                RegrowthPerSecond = regrowthPerSecond,
            });
            writer.Add(new Faction { Value = Faction.Neutral });
        }

        public class Baker : Baker<ResourceNodeAuthoring>
        {
            public override void Bake(ResourceNodeAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                ResourceNodeAuthoring.Add(ref writer, authoring.type, authoring.amount, authoring.regrowthPerSecond);
            }
        }
    }
}
