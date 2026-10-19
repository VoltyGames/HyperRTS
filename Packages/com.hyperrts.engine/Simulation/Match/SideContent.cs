using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>What a side (the game's army id, see <see cref="PlayerSlot.side"/>) starts with and how its AI opens.</summary>
    [Serializable]
    public class SideContent
    {
        [Tooltip("The game's army id; above 0.")]
        public byte side = 1;

        [Tooltip("Spawned at each of this side's players' spawns, owned by the player.")]
        public List<StartKitItem> startKit = new();

        [Tooltip("AI opening of this side's bots; empty trains freely from the start.")]
        public AIBuildOrder buildOrder;
    }
}
