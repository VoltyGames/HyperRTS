using System.Collections.Generic;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// The game's map vocabulary: what each spec <c>kind</c> places, what every player starts with, and the
    /// settlement kits. Exactly one per project. Prefabs with HyperRTS authoring go into the SubScene; the rest are
    /// scenery.
    /// </summary>
    [CreateAssetMenu(menuName = "HyperRTS/Maps/Map Palette", fileName = "MapPalette")]
    public sealed class MapPalette : ScriptableObject
    {
        [Tooltip("Camera and HUD rig every map gets (the game's variant of the RTSWorld rig).")]
        public GameObject rig;

        [Tooltip("Prefab per spec kind (props, resources, bridges, line and scatter props, settlement buildings).")]
        public List<MapPaletteEntry> entries = new();

        [Tooltip("Placed for every spawn, owned by that slot. Offsets are metres right (x) and forward (y) of the spawn.")]
        public List<MapStartItem> startKit = new();

        [Tooltip("Every slot's starting stock.")]
        public List<ResourceQuantity> startingResources = new();

        [Tooltip("Ground levelled and kept clear of settlements, trees and scenery around each spawn (radius, metres).")]
        public float baseRadius = 30f;

        public List<MapSettlementKit> settlementKits = new();

        public MapPaletteEntry Entry(string key) => MapKeys.Get(entries, key, this, "entry");

        public MapSettlementKit SettlementKit(string key) => MapKeys.Get(settlementKits, key, this, "settlement kit");
    }
}
