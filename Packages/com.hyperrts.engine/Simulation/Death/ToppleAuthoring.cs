using HyperRTS.Simulation.Common.Authoring;
using System;
using HyperRTS.Simulation.Destruction;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Death
{
    /// <summary>Destructible scenery that falls away from what killed it, such as a tree.</summary>
    [AddComponentMenu(HyperRTSMenu.Destruction + "Topple")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Destruction)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(DestructibleAuthoring), "a Destructible")]
    public class ToppleAuthoring : AuthoringBehaviour
    {
        [Tooltip("From the trunk's base at the origin to its top in the standing model; its length sets the fall speed.")]
        public Vector3 trunk = Vector3.up;

        [Tooltip("Heading in degrees from +Z toward +X that the canopy points in the remains model.")]
        public float lie;

        [Tooltip("The material of the trunk faces in the standing model's LOD0, which Measure from model reads. Editor only.")]
        public Material trunkMaterial;

        /// <summary><paramref name="trunk"/> is a unit vector.</summary>
        public static void AddTopple(EntityWriter writer, float3 trunk, float height, float lie,
            float canopyRadius)
        {
            writer.Add(new Topple { Trunk = trunk, Height = height, Lie = lie, CanopyRadius = canopyRadius });
            writer.Add<LethalHit>();
        }

        public class Baker : Baker<ToppleAuthoring>
        {
            public override void Bake(ToppleAuthoring authoring)
            {
                if (authoring.trunk.sqrMagnitude < 1e-6f)
                {
                    throw new InvalidOperationException($"Topple on '{authoring.name}' has a zero 'trunk'.");
                }

                var writer = this.Writer();
                var trunk = math.normalize((float3)authoring.trunk);
                AddTopple(writer, trunk, authoring.trunk.magnitude, math.radians(authoring.lie),
                    CanopyRadius(authoring.transform));
            }

            // The widest reach of the standing model's meshes from the stump, so a site clears a canopy over it.
            private float CanopyRadius(Transform root)
            {
                var canopy = 0f;
                foreach (var filter in GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = DependsOn(filter.sharedMesh);
                    if (mesh == null)
                    {
                        continue;
                    }

                    var bounds = mesh.bounds;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var sign = new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1) * 2f - Vector3.one;
                        var point = bounds.center + Vector3.Scale(bounds.extents, sign);
                        var local = root.InverseTransformPoint(filter.transform.TransformPoint(point));
                        canopy = math.max(canopy, math.max(math.abs(local.x), math.abs(local.z)));
                    }
                }

                return canopy;
            }
        }
    }
}
