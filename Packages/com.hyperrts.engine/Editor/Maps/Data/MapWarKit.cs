using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// The old scars a climate's land shows for a map's war age: craters, burnt patches, shredded trees, trenches
    /// along belts and wrecks.
    /// </summary>
    [Serializable]
    [MapLibraryItem("war")]
    public sealed class MapWarKit : IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Header("Ages")]
        public MapWarDensity skirmished = new();
        public MapWarDensity contested = new();
        public MapWarDensity frontline = new();

        [Header("Craters")]
        [Tooltip("Random bowl radius in metres between x and y.")]
        public Vector2 craterRadius = new(4f, 7f);

        [Tooltip("Random bowl depth in metres between x and y; old craters are shallow and soft.")]
        public Vector2 craterDepth = new(0.3f, 0.8f);

        [MapKey(typeof(MapClimateDecal))]
        public string craterDecal;

        [Tooltip("Decal diameter as a share of the bowl's.")]
        public float craterDecalSize = 1.2f;

        [Tooltip("Craters at least this deep (metres) hold water after rain.")]
        public float wetDepth = 0.6f;

        [MapKey(typeof(MapClimateDecal))]
        public string wetDecal;

        [Tooltip("Share of the plants left at the bottom of a bowl; they grow back towards the rim.")]
        [Range(0f, 1f)]
        public float bowlPlants = 0.3f;

        [Tooltip("Climate plant growing in a ring round each bowl; empty for none.")]
        [MapKey(typeof(MapClimatePlant))]
        public string weedPlant;

        [Tooltip("Width of the weed ring outside the rim (metres).")]
        public float weedRing = 2.5f;

        [Range(0f, 1f)]
        public float weedCoverage = 0.8f;

        [Header("Trees")]
        public List<MapWarTreeSwap> shreddedTrees = new();

        [Header("Trenches")]
        [Tooltip("Palette props laid end to end along belts; empty for none.")]
        [MapKey(typeof(MapProp))]
        public List<string> trenchProps = new();

        [Tooltip("Distance from the belt's edge to the trench (metres).")]
        public float trenchOffset = 5f;

        [Tooltip("Random trench length in metres between x and y.")]
        public Vector2 trenchLength = new(20f, 50f);

        [Header("Burnt ground")]
        [MapKey(typeof(MapClimateLayer))]
        public string burntLayer;

        [Tooltip("Random patch radius in metres between x and y.")]
        public Vector2 burntRadius = new(8f, 20f);

        [Header("Wrecks")]
        [MapKey(typeof(MapProp))]
        public List<string> wrecks = new();

        [Tooltip("Wrecks lie at most this far (metres) from belts and woods, out of the open lanes.")]
        public float wreckCoverDistance = 8f;

        public MapWarDensity Density(MapWarAge age) => age switch
        {
            MapWarAge.Skirmished => skirmished,
            MapWarAge.Contested => contested,
            MapWarAge.Frontline => frontline,
            _ => throw new ArgumentOutOfRangeException(nameof(age), age, "HyperRTS: an untouched map has no war density."),
        };

        /// <summary>The shredded stand-ins for a climate tree, or null when it has none.</summary>
        public MapWarTreeSwap Swap(string tree) => shreddedTrees.Find(swap => swap.tree == tree);

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs) => this.key = key;
    }
}
