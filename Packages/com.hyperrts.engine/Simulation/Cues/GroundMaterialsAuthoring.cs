using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>Bakes the map's ground material picture into the <see cref="GroundMaterials"/> singleton. One per match.</summary>
    [AddComponentMenu(HyperRTSMenu.Effects + "Ground Materials")]
    [Icon(HyperRTSIcons.Effects)]
    [HelpURL(HyperRTSDocs.Effects)]
    [DisallowMultipleComponent]
    public class GroundMaterialsAuthoring : AuthoringBehaviour
    {
        [Tooltip("Readable R8 picture, one SurfaceMaterial byte per cell, row 0 south. This transform is its south-west corner.")]
        public Texture2D map;

        [Tooltip("Width of one cell (metres).")]
        public float spacing = 1f;

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
                Require(map.isReadable, map, "must be readable");
                Require(map.format == TextureFormat.R8, map, $"must be R8, not {map.format}");
                Require(authoring.spacing > 0f, map, $"needs a spacing above 0, not {authoring.spacing}");
                var position = GetComponent<Transform>().position;
                var materials = GroundMaterials.Create(map.GetPixelData<byte>(0), new int2(map.width, map.height),
                    new float2(position.x, position.z), authoring.spacing, Allocator.Persistent);
                AddBlobAsset(ref materials.Blob, out _);
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.None));
                writer.Add(materials);
            }

            private static void Require(bool ok, Texture2D map, string problem)
            {
                if (!ok)
                {
                    throw new InvalidOperationException($"HyperRTS: ground material map '{map.name}' {problem}.");
                }
            }
        }
    }
}
