using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A ground decal maps lay at build time (a puddle, leaf litter, an oil stain): its material and size.</summary>
    [Serializable]
    [MapLibraryItem("decal", "material")]
    public sealed class MapClimateDecal : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("Decal material (URP decal shader).")]
        [NonSerialized]
        public Material material;

        [Tooltip("Random diameter in metres between x and y.")]
        public Vector2 size = new(1f, 2f);

        [Tooltip("Turn each surface decal to a random yaw; off faces north. Decals under props turn with the prop.")]
        public bool randomRotation = true;

        [Tooltip("How far the decal reaches above and below the ground (metres); shallow keeps units standing in it clean.")]
        public float depth = 0.5f;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            this.key = key;
            material = refs.Asset<Material>(refs.material, "material");
        }
    }
}
