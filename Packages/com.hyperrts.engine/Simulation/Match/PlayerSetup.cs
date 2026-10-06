using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>One player slot in <see cref="MatchAuthoring"/>; its list position + 1 is the faction number.</summary>
    [Serializable]
    public class PlayerSetup
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

        /// <summary>Adds the components every player entity carries; returns its empty stockpile.</summary>
        public static DynamicBuffer<ResourceStock> Add<TSink>(ref TSink sink, byte faction, in FixedString32Bytes name,
            float4 color, int populationCap) where TSink : struct, IComponentSink
        {
            sink.Add(new Player { Faction = faction, Name = name, Color = color });
            sink.Add(new Population { Cap = populationCap });
            sink.Add<PowerGrid>();
            sink.Add<Defeated>();
            sink.SetEnabled<Defeated>(false);
            sink.AddBuffer<PlayerCommand>();
            sink.AddBuffer<ResearchedUpgrade>();
            return sink.AddBuffer<ResourceStock>();
        }
    }
}
