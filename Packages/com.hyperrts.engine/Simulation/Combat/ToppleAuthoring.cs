using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Destructible scenery that falls away from what killed it, such as a tree.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Topple")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(DestructibleAuthoring), "a Destructible")]
    public class ToppleAuthoring : AuthoringBehaviour
    {
        [Tooltip("From the trunk's base at the origin to its top in the standing model; its length sets the fall speed.")]
        public Vector3 trunk = Vector3.up;

        [Tooltip("Heading in degrees from +Z toward +X that the crown points in the remains model.")]
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
                DestructibleSetup.AddTopple(ref writer, trunk, authoring.trunk.magnitude, math.radians(authoring.lie));
            }
        }
    }
}
