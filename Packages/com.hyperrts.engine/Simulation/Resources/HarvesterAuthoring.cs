using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>Lets a unit gather from resource nodes and carry cargo to a drop-off.</summary>
    [AddComponentMenu(HyperRTSMenu.Resources + "Harvester")]
    [Icon(HyperRTSIcons.Resources)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class HarvesterAuthoring : AuthoringBehaviour
    {
        [Tooltip("Cargo carried per trip.")]
        [Min(1)]
        public int capacity = 10;

        [Tooltip("Amount gathered per second while at a node.")]
        [Min(0.01f)]
        public float gatherRate = 2f;

        public static void Add<TWriter>(ref TWriter writer, int capacity, float gatherRate)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Harvester { Capacity = capacity, GatherRate = gatherRate });
            writer.Add<HarvestState>();
        }

        public class Baker : Baker<HarvesterAuthoring>
        {
            public override void Bake(HarvesterAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                HarvesterAuthoring.Add(ref writer, authoring.capacity, authoring.gatherRate);
            }
        }
    }
}
