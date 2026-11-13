using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Resources.Gathering
{
    /// <summary>Lets a unit gather from resource nodes and carry cargo to a drop-off.</summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Resources/Harvester")]
    [Icon(HyperRTSIcons.HarvesterAuthoring)]
    [HelpURL(HyperRTSDocs.Resources)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class HarvesterAuthoring : AuthoringBehaviour
    {
        [Tooltip("Cargo carried per trip.")]
        [Min(1)]
        public int capacity = 10;

        [Tooltip("Amount gathered while at a node.")]
        [Unit("/s")]
        [Min(0.01f)]
        public float gatherRate = 2f;

        [Tooltip("How far from a depleted node it looks for another of the same type.")]
        [Unit("m")]
        [Min(0f)]
        public float replacementRange = 30f;

        [Tooltip("Seconds it stands at the drop-off before the cargo is paid, so an unload can be shown. 0 pays on arrival.")]
        [Unit("s")]
        [Min(0f)]
        public float unloadSeconds;

        public static void Add(EntityWriter writer, int capacity, float gatherRate, float replacementRange,
            float unloadSeconds)
        {
            writer.Add(new Harvester
            {
                Capacity = capacity, GatherRate = gatherRate, ReplacementRange = replacementRange,
                UnloadSeconds = unloadSeconds,
            });
            writer.Add<HarvestState>();
        }

        public class Baker : Baker<HarvesterAuthoring>
        {
            public override void Bake(HarvesterAuthoring authoring)
            {
                var writer = this.Writer();
                HarvesterAuthoring.Add(writer, authoring.capacity, authoring.gatherRate,
                    authoring.replacementRange, authoring.unloadSeconds);
            }
        }
    }
}
