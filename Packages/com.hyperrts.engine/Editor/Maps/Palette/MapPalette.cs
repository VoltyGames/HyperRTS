using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Palette
{
    /// <summary>
    /// The game's map vocabulary: what each spec <c>kind</c> places, what each side starts with, and the
    /// settlement kits. Exactly one <c>*.palette.asset</c> per project; its entries are every library prop and its
    /// settlement kits every library settlement (see <see cref="MapLibrary.Palette"/>). Prefabs with HyperRTS authoring
    /// go into the SubScene; the rest are scenery.
    /// </summary>
    [MapLibraryItem("palette")]
    [CreateAssetMenu(menuName = HyperRTSMenu.MapLibrary + "Palette", fileName = "New.palette")]
    public sealed class MapPalette : MapLibraryItem
    {
        [Tooltip("Camera and HUD rig every map gets (the game's variant of the RTSWorld rig).")]
        public GameObject rig;

        [Tooltip("Prefab per spec kind (props, resources, bridges, line and scatter props, settlement buildings).")]
        [NonSerialized]
        public List<MapProp> entries = new();

        [Tooltip("Start kit and AI build order per side. Slot n plays sides[(n - 1) % count] when its scene plays as baked.")]
        public List<MapPaletteSide> sides = new();

        [Tooltip("Every slot's starting stock.")]
        public List<ResourceQuantity> startingResources = new();

        [Tooltip("Ground levelled and kept clear of settlements, trees and scenery around each spawn (radius, metres).")]
        public float baseRadius = 30f;

        [NonSerialized]
        public List<MapSettlementKit> settlementKits = new();

        public MapProp Entry(string key) => MapKeys.Get(entries, key, this.key, "entry");

        public MapSettlementKit SettlementKit(string key) => MapKeys.Get(settlementKits, key, this.key, "settlement kit");
    }
}
