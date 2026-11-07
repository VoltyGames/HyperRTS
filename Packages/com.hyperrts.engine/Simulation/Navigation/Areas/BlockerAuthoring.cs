using HyperRTS.Simulation.Common.Authoring;
using System;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation.Areas
{
    /// <summary>Makes remains such as a wreck a fixed blocker units go round, until Decay starts to bury it.</summary>
    [AddComponentMenu(HyperRTSMenu.Navigation + "Blocker")]
    [Icon(HyperRTSIcons.Navigation)]
    [HelpURL(HyperRTSDocs.Movement)]
    [DisallowMultipleComponent]
    public class BlockerAuthoring : AuthoringBehaviour
    {
        [Tooltip("Half the blocker's width: match the hull it lies in.")]
        [Unit("m")]
        [Min(0.01f)]
        public float radius = 1f;

        [Tooltip("Full blocker length along this transform's forward. 0 (or up to the diameter) keeps a circle.")]
        [Unit("m")]
        [Min(0f)]
        public float hullLength;

        public static void Add(EntityWriter writer, string owner, float radius, float hullLength)
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
                var writer = this.Writer();
                BlockerAuthoring.Add(writer, authoring.name, authoring.radius, authoring.hullLength);
            }
        }
    }
}
