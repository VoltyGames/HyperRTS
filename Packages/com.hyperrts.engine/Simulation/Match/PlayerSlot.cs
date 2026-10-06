using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>One player slot in <see cref="MatchAuthoring"/>; its list position + 1 is the faction number.</summary>
    [Serializable]
    public class PlayerSlot
    {
        /// <summary>Distinct default colours, one per player slot.</summary>
        public static readonly Color[] Palette =
        {
            new(0.2f, 0.45f, 1f), new(0.9f, 0.2f, 0.15f), new(0.25f, 0.8f, 0.3f), new(0.95f, 0.8f, 0.2f),
            new(0.6f, 0.3f, 0.9f), new(1f, 0.55f, 0.15f), new(0.2f, 0.8f, 0.8f), new(0.95f, 0.45f, 0.7f),
        };

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

        [Tooltip("AI only: tuning preset from the Match's AI settings.")]
        public AIDifficulty difficulty = AIDifficulty.Normal;

        [Tooltip("AI only: opening to build before training freely; empty trains from the start.")]
        public AIBuildOrder buildOrder;
    }
}
