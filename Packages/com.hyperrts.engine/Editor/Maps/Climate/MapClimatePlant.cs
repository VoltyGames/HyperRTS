using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Presentation.Environment.Vegetation.Grass;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>A grass or small plant the map's <see cref="GrassRenderer"/> scatters: its models, material and sizes.</summary>
    [MapLibraryItem("plant")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Plant", fileName = "New.plant")]
    public sealed class MapClimatePlant : MapLibraryItem
    {
        [Tooltip("The plant's models, each an FBX or mesh asset with one submesh, origin at the root.")]
        public List<Mesh> meshes = new();

        [Tooltip("HyperRTS/Foliage material the models share.")]
        public Material material;

        [Tooltip("Random horizontal scale range of the models: metres for a card plant on 1 m card meshes.")]
        public Vector2 width = new(0.85f, 1.15f);

        [Tooltip("Random vertical scale range of the models: metres for a card plant on 1 m card meshes.")]
        public Vector2 height = new(0.85f, 1.15f);

        public Color healthy = Color.white;
        public Color dry = new(0.92f, 0.86f, 0.7f);

        [Tooltip("Plants per square metre at full coverage.")]
        public float density = 2f;

        [Tooltip("How strongly the plants gather into clumps and tufts: 0 spreads them evenly.")]
        [Range(0f, 1f)]
        public float clumping = 0.5f;

        [Tooltip("Metres the plant draws to; 0 draws it to the quality distance. A short reach adds close-up detail, " +
                 "such as a dense low sward, without the far cost.")]
        [Min(0f)]
        public float reach;

        [Tooltip("What quality level scales the plant by: Ground always, Close only while close detail is above 0, " +
                 "Clutter in proportion to close detail.")]
        public PlantKind kind;

        protected internal override void Load()
        {
            RequireFilled(meshes, "meshes");
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
            reach = reach,
            kind = kind,
        };
    }
}
