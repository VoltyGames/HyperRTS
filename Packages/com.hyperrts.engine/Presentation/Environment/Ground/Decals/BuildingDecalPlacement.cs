using System;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Decals
{
    /// <summary>Where one of a building's decals lies on its apron, as listed on <see cref="BuildingDecalsAuthoring"/>.</summary>
    [Serializable]
    public struct BuildingDecalPlacement
    {
        [Tooltip("Decal material projected onto the apron and the ground round it.")]
        public Material material;

        [Tooltip("Centre of the decal in the building's space; y at the apron's top.")]
        public Vector3 position;

        [Tooltip("Width and length on the ground; the texture's top runs along the length.")]
        [Unit("m")]
        public Vector2 size;

        [Tooltip("Turn of the texture's top clockwise from the building's front.")]
        [Unit("°")]
        [Range(-180f, 180f)]
        public float yaw;
    }
}
