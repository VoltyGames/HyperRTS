using System;
using HyperRTS.Editor.Maps.Library;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// How a settlement type lays out its plots along a street: a house behind the front fence and gate, outbuildings
    /// behind it, a vegetable garden, an orchard or meadow at the back, fences between plots, and street furniture.
    /// Building and fence keys are props; surfaces and trees are climate keys.
    /// </summary>
    [Serializable]
    [MapLibraryItem("settlement")]
    public sealed class MapSettlementKit : IMapKeyed, IMapLibraryItem
    {
        [NonSerialized]
        public string key;

        [Tooltip("Plot width along the street (x) and depth away from it (y), in metres.")]
        public Vector2 plot = new(26f, 70f);

        [Tooltip("Plot widths vary by up to this share either way.")]
        [Range(0f, 0.4f)]
        public float plotWidthVariation = 0.15f;

        [Tooltip("Gap between the road edge and the front fence (metres).")]
        public float frontGap = 2f;

        [Tooltip("Depth of the front yard between the front fence and the house (metres).")]
        public float frontYard = 5f;

        [Tooltip("Palette keys of the houses, picked in turn with some shuffling.")]
        [MapKey(typeof(MapProp))]
        public List<string> houses = new();

        [Tooltip("Palette keys of outbuildings (summer kitchens, sheds, barns) placed behind the house.")]
        [MapKey(typeof(MapProp))]
        public List<string> outbuildings = new();

        [Min(0)]
        public int maxOutbuildings = 2;

        [Tooltip("Palette keys of the fence sections along the street, between plots, and the gate.")]
        [MapKey(typeof(MapProp))]
        public string frontFence;

        [MapKey(typeof(MapProp))]

        public string sideFence;
        [MapKey(typeof(MapProp))]
        public string gate;

        [Tooltip("Climate surfaces of the yard, the vegetable garden and the back of the plot.")]
        [MapKey(typeof(MapClimateSurface))]
        public string yardSurface;

        [MapKey(typeof(MapClimateSurface))]

        public string gardenSurface;
        [MapKey(typeof(MapClimateSurface))]
        public string backSurface;

        [Tooltip("Share of the depth behind the yard that is vegetable garden; the rest is the back.")]
        [Range(0f, 1f)]
        public float gardenShare = 0.55f;

        [Tooltip("Climate tree of the orchard at the back of the plot; empty leaves it meadow.")]
        [MapKey(typeof(MapClimateTree))]
        public string orchardTree;

        public float orchardSpacing = 6f;

        public List<MapPlotProp> extras = new();

        [Tooltip("Palette key of a pipe section run along the plot fronts (gas pipe); empty for none.")]
        [MapKey(typeof(MapProp))]
        public string streetPipe;

        [Tooltip("Palette key of the arch that carries the pipe over a driveway or a crossing road; required with a pipe.")]
        [MapKey(typeof(MapProp))]
        public string streetPipeArch;

        [Tooltip("Fewest pipe sections in a run; shorter runs are left out. Required (at least 1) with a pipe.")]
        public int streetPipeMinSections;

        [Tooltip("Widest gap (metres) inside a pipe run an arch bridges, stretched to fit; wider gaps end the run. Required (above 0) with a pipe.")]
        public float streetPipeMaxGap;

        [Tooltip("Palette key of the poles along the street; empty for none.")]
        [MapKey(typeof(MapProp))]
        public string streetPole;

        public float poleSpacing = 35f;

        string IMapKeyed.Key => key;

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs) => this.key = key;
    }
}
