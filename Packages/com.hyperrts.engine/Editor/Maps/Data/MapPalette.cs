using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// The game's map vocabulary: what each spec <c>kind</c> places, what every player starts with, and the
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

        [Tooltip("Placed for every spawn, owned by that slot. Offsets are metres right (x) and forward (y) of the spawn.")]
        public List<MapStartItem> startKit = new();

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
        }
    }
}
