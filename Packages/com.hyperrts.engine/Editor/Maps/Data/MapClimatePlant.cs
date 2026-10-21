using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Presentation.Environment.Grass;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A grass or small plant the map's <see cref="GrassRenderer"/> scatters: its models, material and sizes.</summary>
    [Serializable]
    [MapLibraryItem("plant", "meshes", "material")]
    public sealed class MapClimatePlant : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("The plant's models, each an FBX or mesh asset with one submesh, origin at the root.")]
        [NonSerialized]
        public List<Mesh> meshes = new();

        [Tooltip("HyperRTS/Foliage material the models share.")]
        [NonSerialized]
        public Material material;

        [Tooltip("Random horizontal scale range of the models.")]
        public Vector2 width = new(0.85f, 1.15f);

        [Tooltip("Random vertical scale range of the models.")]
        public Vector2 height = new(0.85f, 1.15f);

        public Color healthy = Color.white;
        public Color dry = new(0.92f, 0.86f, 0.7f);

        [Tooltip("Plants per square metre at full coverage.")]
        public float density = 2f;

        [Tooltip("How strongly the plants gather into clumps and tufts: 0 spreads them evenly.")]
        [Range(0f, 1f)]
        public float clumping = 0.5f;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            this.key = key;
            meshes = refs.Assets<Mesh>(refs.meshes, "meshes");
            material = refs.Asset<Material>(refs.material, "material");
        }

        /// <summary>The plant as the renderer draws it.</summary>
        public GrassPlant Plant() => new()
        {
            name = key,
            meshes = new List<Mesh>(meshes),
            material = material,
            width = width,
            height = height,
            healthy = healthy,
            dry = dry,
            density = density,
            clumping = clumping,
        };
    }
}
