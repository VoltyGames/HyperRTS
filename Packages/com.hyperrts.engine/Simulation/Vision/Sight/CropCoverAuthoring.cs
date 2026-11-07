using System;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>Bakes the map's <see cref="CropCoverMap"/> into the <see cref="CropCover"/> singleton. One per match.</summary>
    [AddComponentMenu(HyperRTSMenu.Scene + "Vision/Crop Cover")]
    [Icon(HyperRTSIcons.CropCoverAuthoring)]
    [HelpURL(HyperRTSDocs.Vision)]
    [DisallowMultipleComponent]
    public class CropCoverAuthoring : AuthoringBehaviour
    {
        [Tooltip("The map's crop cover grid. This transform is its south-west corner.")]
        public CropCoverMap map;

        public class Baker : Baker<CropCoverAuthoring>
        {
            public override void Bake(CropCoverAuthoring authoring)
            {
                var map = authoring.map;
                if (map == null)
                {
                    throw new InvalidOperationException($"HyperRTS: Crop Cover on '{authoring.name}' has no map.");
                }

                DependsOn(map);
                var position = GetComponent<Transform>().position;
                var cover = map.Bake(new float2(position.x, position.z), Allocator.Persistent);
                AddBlobAsset(ref cover.Blob, out _);
                var writer = this.Writer(TransformUsageFlags.None);
                writer.Add(cover);
            }
        }
    }
}
