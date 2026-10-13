using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Lets a vehicle leave ruts and flatten grass where the local player sees it drive.</summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Track Marks")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Units)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class TrackMarksAuthoring : AuthoringBehaviour
    {
        [Tooltip("Width of one track or tyre in metres.")]
        [Min(0.05f)]
        public float width = 0.6f;

        [Tooltip("Distance between the left and right track centres in metres.")]
        [Min(0.1f)]
        public float gauge = 2.8f;

        [Tooltip("How deep the ruts read: about 0.3 for a light car, 1 for a tank.")]
        [Range(0f, 1f)]
        public float rutDepth = 1f;

        public class Baker : Baker<TrackMarksAuthoring>
        {
            public override void Bake(TrackMarksAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.Add(new TrackMarks
                {
                    Width = authoring.width,
                    Gauge = authoring.gauge,
                    RutDepth = authoring.rutDepth,
                });
            }
        }
    }
}
