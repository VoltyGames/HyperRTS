using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Presentation.Environment.Grass;
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

        [Tooltip("A card plant's <Key>.card.json, the size measured from its render, which width and height scale. Empty for modelled plants.")]
        public TextAsset cardFile;

        /// <summary>The size read from <see cref="cardFile"/> once loaded; null for a modelled plant.</summary>
        [NonSerialized]
        public MapPlantCard card;

        [Tooltip("Random horizontal scale range of the models, times the card width for a card plant.")]
        public Vector2 width = new(0.85f, 1.15f);

        [Tooltip("Random vertical scale range of the models, times the card height for a card plant.")]
        public Vector2 height = new(0.85f, 1.15f);

        public Color healthy = Color.white;
        public Color dry = new(0.92f, 0.86f, 0.7f);

        [Tooltip("Plants per square metre at full coverage.")]
        public float density = 2f;

        [Tooltip("How strongly the plants gather into clumps and tufts: 0 spreads them evenly.")]
        [Range(0f, 1f)]
        public float clumping = 0.5f;

        protected internal override void Load()
        {
            RequireFilled(meshes, "meshes");
            card = cardFile == null ? null : MapPlantCard.Read(cardFile);
        }

        /// <summary>The plant as the renderer draws it.</summary>
        public GrassPlant Plant() => new()
        {
            name = key,
            meshes = new List<Mesh>(meshes),
            material = material,
            width = card == null ? width : width * card.width,
            height = card == null ? height : height * card.height,
            healthy = healthy,
            dry = dry,
            density = density,
            clumping = clumping,
        };
    }
}
