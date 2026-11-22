using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Decals
{
    /// <summary>A ground decal a building carries on its apron, such as an oil stain or tyre tracks at its door.</summary>
    public struct BuildingDecal : IBufferElementData
    {
        public UnityObjectRef<Material> Material;

        /// <summary>Centre of the projection box in the building's space.</summary>
        public float3 Position;

        /// <summary>Width and length on the ground, then the projection depth.</summary>
        public float3 Size;

        /// <summary>Turn of the texture's top clockwise from the building's front, in radians.</summary>
        public float Yaw;

        /// <summary>Projecting down the building's up axis, the texture's top turned by <see cref="Yaw"/>.</summary>
        public readonly quaternion Facing =>
            quaternion.LookRotationSafe(new float3(0f, -1f, 0f), new float3(math.sin(Yaw), 0f, math.cos(Yaw)));
    }
}
