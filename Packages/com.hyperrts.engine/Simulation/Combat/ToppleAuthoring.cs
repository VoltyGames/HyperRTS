using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Destructible scenery that falls over away from what killed it (a tree): its remains lie along the fall, and
    /// presentation shows the fall when someone sees it.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Topple")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(DestructibleAuthoring), "a Destructible")]
    public class ToppleAuthoring : AuthoringBehaviour
    {
        [Tooltip("Trunk direction in the standing model, from its base at the origin to the top of the trunk.")]
        public Vector3 trunk = Vector3.up;

        [Tooltip("Heading in degrees (from +Z toward +X) the crown points in the remains model.")]
        public float lie;

        public class Baker : Baker<ToppleAuthoring>
        {
            public override void Bake(ToppleAuthoring authoring)
            {
                if (authoring.trunk.sqrMagnitude < 1e-6f)
                {
                    throw new InvalidOperationException($"Topple on '{authoring.name}' has a zero 'trunk'.");
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                var trunk = math.normalize((float3)authoring.trunk);
                DestructibleSetup.AddTopple(ref writer, trunk, math.radians(authoring.lie));
            }
        }
    }
}
