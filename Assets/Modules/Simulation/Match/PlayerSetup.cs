using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>One player slot in <see cref="MatchAuthoring"/>; its list position + 1 is the faction number.</summary>
    [Serializable]
    public class PlayerSetup
    {
        [Tooltip("Name shown in the HUD.")]
        public string name = "Player";

        [Tooltip("Players on the same team are allies.")]
        [Range(1, FactionRelations.MaxTeams - 1)]
        public int team = 1;

        [Tooltip("Team colour applied to owned units and buildings.")]
        public Color color = Color.blue;

        [Tooltip("Who drives this player.")]
        public PlayerControl control = PlayerControl.LocalHuman;

        [Tooltip("Stockpile at match start.")]
        public List<ResourceQuantity> startingResources = new();
    }
}
