using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// The game's map vocabulary: what each spec <c>kind</c> places, what each side starts with, and the
    /// settlement kits. Exactly one <c>*.palette.json</c> per project; its entries are every library prop and its
    /// settlement kits every library settlement (see <see cref="MapLibrary.Palette"/>). Prefabs with HyperRTS authoring
    /// go into the SubScene; the rest are scenery.
    /// </summary>
    [Serializable]
    [MapLibraryItem("palette", "rig", "startingResources")]
    public sealed class MapPalette : IMapLibraryItem
    {
        [NonSerialized]
        public string name;

        [Tooltip("Camera and HUD rig every map gets (the game's variant of the RTSWorld rig).")]
        [NonSerialized]
        public GameObject rig;

        [Tooltip("Prefab per spec kind (props, resources, bridges, line and scatter props, settlement buildings).")]
        [NonSerialized]
        public List<MapPaletteEntry> entries = new();

        [Tooltip("Start kit and AI build order per side. Slot n plays sides[(n - 1) % count] when its scene plays as baked.")]
        public List<MapPaletteSide> sides = new();

        [Tooltip("Every slot's starting stock.")]
        [NonSerialized]
        public List<ResourceQuantity> startingResources = new();

        [Tooltip("Ground levelled and kept clear of settlements, trees and scenery around each spawn (radius, metres).")]
        public float baseRadius = 30f;

        [NonSerialized]
        public List<MapSettlementKit> settlementKits = new();

        public MapPaletteEntry Entry(string key) => MapKeys.Get(entries, key, name, "entry");

        public MapSettlementKit SettlementKit(string key) => MapKeys.Get(settlementKits, key, name, "settlement kit");

        void IMapLibraryItem.Load(string key, MapLibraryRefs refs)
        {
            name = key;
            rig = refs.Asset<GameObject>(refs.rig, "rig");
            startingResources = refs.startingResources.ConvertAll(resource => new ResourceQuantity
            {
                type = refs.Asset<ResourceType>(resource.type, "startingResources.type"),
                amount = resource.amount,
            });
            for (var i = 0; i < sides.Count; i++)
            {
                sides[i].buildOrderAsset = refs.Asset<AIBuildOrder>(sides[i].buildOrder, $"sides[{i}].buildOrder");
            }
        }
    }
}
