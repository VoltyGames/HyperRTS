using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Makes remains such as a wreck a blocker: units slide round its capsule instead of driving into it. It never moves
    /// and stops blocking once it sinks under Decay.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Navigation + "Blocker")]
    [Icon(HyperRTSIcons.Navigation)]
    [HelpURL(HyperRTSDocs.Units)]
    [DisallowMultipleComponent]
    public class BlockerAuthoring : AuthoringBehaviour
    {
        [Tooltip("Half the blocker's width in metres: match the hull it lies in.")]
        [Min(0.01f)]
        public float radius = 1f;

        [Tooltip("Full blocker length along this transform's forward, in metres. 0 (or up to the diameter) keeps a circle.")]
        [Min(0f)]
        public float hullLength;

        public static void Add<TWriter>(ref TWriter writer, string owner, float radius, float hullLength)
            where TWriter : struct, IEntityWriter
        {
            if (radius <= 0f)
            {
                throw new ArgumentException($"Blocker on '{owner}': radius must be above 0.");
            }

            writer.Add(new Blocker { Radius = radius, HullLength = hullLength });
        }

        public class Baker : Baker<BlockerAuthoring>
        {
            public override void Bake(BlockerAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                BlockerAuthoring.Add(ref writer, authoring.name, authoring.radius, authoring.hullLength);
            }
        }
    }
}
