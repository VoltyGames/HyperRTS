using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Bakes the map's <see cref="GroundMaterialMap"/> into the <see cref="GroundMaterials"/> singleton. One per match.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Ground Materials")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class GroundMaterialsAuthoring : AuthoringBehaviour
    {
        [Tooltip("The map's ground grid. This transform is its south-west corner.")]
        public GroundMaterialMap map;

        public class Baker : Baker<GroundMaterialsAuthoring>
        {
            public override void Bake(GroundMaterialsAuthoring authoring)
            {
                var map = authoring.map;
                if (map == null)
                {
                    throw new InvalidOperationException($"HyperRTS: Ground Materials on '{authoring.name}' has no map.");
                }

                DependsOn(map);
                var position = GetComponent<Transform>().position;
                var materials = map.Bake(new float2(position.x, position.z), Allocator.Persistent);
                AddBlobAsset(ref materials.Blob, out _);
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.None));
                writer.Add(materials);
            }
        }
    }
}
