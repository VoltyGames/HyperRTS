using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>A ground decal maps lay at build time (a puddle, leaf litter, an oil stain): its material and size.</summary>
    [MapLibraryItem("decal")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Decal", fileName = "New.decal")]
    public sealed class MapClimateDecal : MapLibraryItem
    {
        [Tooltip("Decal material (URP decal shader).")]
        public Material material;

        [Tooltip("Random diameter in metres between x and y.")]
        public Vector2 size = new(1f, 2f);

        [Tooltip("Turn each surface decal to a random yaw; off faces north. Decals under props turn with the prop.")]
        public bool randomRotation = true;

        [Tooltip("Centre each surface decal on the nearest road or track, its long axis along it (texture V); needs randomRotation off.")]
        public bool alongRoad;

        [Tooltip("How far the decal reaches above and below the ground (metres); shallow keeps units standing in it clean.")]
        public float depth = 0.5f;
    }
}
