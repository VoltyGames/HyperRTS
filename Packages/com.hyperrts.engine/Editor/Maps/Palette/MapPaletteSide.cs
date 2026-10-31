using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Match;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Palette
{
    /// <summary>What players of one side (the game's army id) start with, and how its bots open.</summary>
    [Serializable]
    public sealed class MapPaletteSide
    {
        [Tooltip("The game's army id, 1 to 255.")]
        public int side = 1;

        [Tooltip("Spawned at each of the side's spawns, owned by the slot. Offsets are metres right (x) and forward (y).")]
        public List<MapStartItem> startKit = new();

        [Tooltip("AI Build Order asset of the side's bots; empty trains freely.")]
        public AIBuildOrder buildOrder;
    }
}
