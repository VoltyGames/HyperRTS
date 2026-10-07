using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Match rules for a map: playable area, grids and players. Place exactly one in the SubScene.</summary>
    [AddComponentMenu(HyperRTSMenu.Match + "Match")]
    [Icon(HyperRTSIcons.Match)]
    [HelpURL(HyperRTSDocs.GettingStarted)]
    [DisallowMultipleComponent]
    [RequiresGhost]
    public class MatchAuthoring : AuthoringBehaviour
    {
        [Header("Map")]
        [Tooltip("Playable area (X by Z) centred on this transform.")]
        public Vector2 mapSize = new(200f, 200f);

        [Tooltip("Pathfinding grid cell size. Smaller is more precise but slower.")]
        [Min(0.25f)]
        public float navCellSize = 1f;

        [Tooltip("Fog-of-war grid cell size.")]
        [Min(0.5f)]
        public float fogCellSize = 2f;

        [Tooltip("Hide what the local player's team can't see.")]
        public bool fogOfWar = true;

        [Tooltip("Height of the water surface (Water nav areas, flooded terrain); ships ride at it.")]
        public float waterLevel;

        [Tooltip("Turn terrain below the water level into water, open to ships only. Off: only Water nav areas are water.")]
        public bool floodBelowWaterLevel;

        [Tooltip("Steepest ground (degrees) units may drive on; steeper Terrain Height cells are blocked. 0 = no limit.")]
        [Range(0f, 90f)]
        public float maxSlope = 45f;

        [Header("Rules")]
        [Tooltip("Production speed of power-consuming producers while their owner's power is low.")]
        [Range(0f, 1f)]
        public float lowPowerProductionRate = 0.5f;

        [Tooltip("Share of a finished building's cost refunded when sold; unfinished buildings refund in full.")]
        [Range(0f, 1f)]
        public float sellRefund = 0.5f;

        [Tooltip("Record a replay from the start of the match; save it with HyperRTS ▸ Replays ▸ Save Recording.")]
        public bool recordReplay;

        [Header("Players")]
        [Tooltip("Player slots. Slot 1 is faction 1, the 'Owner' number on units and buildings.")]
        public List<PlayerSlot> players = new()
        {
            new PlayerSlot { name = "Player", team = 1, color = PlayerSlot.Palette[0] },
            new PlayerSlot { name = "Enemy", team = 2, color = PlayerSlot.Palette[1], control = PlayerControl.AI },
        };

        [Header("AI")]
        [Tooltip("Tuning of AI slots set to Easy.")]
        public AITuning easyAI = new() { thinkInterval = 4f, attackWaveSize = 10, useAbilities = false };

        [Tooltip("Tuning of AI slots set to Normal.")]
        public AITuning normalAI = new() { thinkInterval = 2f, attackWaveSize = 6, useAbilities = true };

        [Tooltip("Tuning of AI slots set to Hard.")]
        public AITuning hardAI = new() { thinkInterval = 1f, attackWaveSize = 4, useAbilities = true };

        [Tooltip("Tuning of AI slots set to Expert.")]
        public AITuning expertAI = new() { thinkInterval = 0.5f, attackWaveSize = 4, useAbilities = true };

        [Tooltip("Tuning of AI slots set to Brutal: Expert play plus an income bonus.")]
        public AITuning brutalAI = new()
        {
            thinkInterval = 0.5f, attackWaveSize = 4, useAbilities = true, incomeMultiplier = 1.25f,
        };

        [Tooltip("Build order of slots without their own, used when a match setup turns a human slot into an AI.")]
        public AIBuildOrder defaultBuildOrder;

        public AITuning AITuningFor(AIDifficulty difficulty) => difficulty switch
        {
            AIDifficulty.Easy => easyAI,
            AIDifficulty.Hard => hardAI,
            AIDifficulty.Expert => expertAI,
            AIDifficulty.Brutal => brutalAI,
            _ => normalAI,
        };

        /// <summary>The map's ground rectangle (X by Z), centred on this transform.</summary>
        public Rect MapRect => new(new Vector2(transform.position.x, transform.position.z) - mapSize * 0.5f, mapSize);

        public class Baker : Baker<MatchAuthoring>
        {
            public override void Bake(MatchAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MapSettings
                {
                    Min = authoring.MapRect.min,
                    Size = authoring.mapSize,
                    NavCellSize = authoring.navCellSize,
                    FogCellSize = authoring.fogCellSize,
                    FogOfWar = authoring.fogOfWar,
                    WaterLevel = authoring.waterLevel,
                    FloodTerrain = authoring.floodBelowWaterLevel,
                    MaxSlope = authoring.maxSlope,
                });
                AddComponent(entity, new MatchState { Phase = MatchPhase.Playing });
                AddComponent(entity, new MatchRules
                {
                    LowPowerProductionRate = authoring.lowPowerProductionRate,
                    SellRefund = authoring.sellRefund,
                    RecordReplay = authoring.recordReplay,
                });

                var relations = new FactionRelations();
                relations.Teams.Add(0);
                for (var i = 0; i < authoring.players.Count; i++)
                {
                    relations.Teams.Add((byte)authoring.players[i].team);
                    BakePlayer(authoring, authoring.players[i], (byte)(i + 1));
                }

                AddComponent(entity, relations);
                AddTunings(entity, authoring);
            }

            // Every difficulty is baked, so a match setup can hand any slot to the AI at runtime.
            private void AddTunings(Entity match, MatchAuthoring authoring)
            {
                var tunings = AddBuffer<AIDifficultyTuning>(match);
                foreach (AIDifficulty difficulty in Enum.GetValues(typeof(AIDifficulty)))
                {
                    tunings.Add(authoring.AITuningFor(difficulty).ToTuning(difficulty));
                }
            }

            private void BakePlayer(MatchAuthoring authoring, PlayerSlot slot, byte faction)
            {
                var player = CreateAdditionalEntity(TransformUsageFlags.None, entityName: slot.name);
                var playerName = new FixedString32Bytes();
                playerName.CopyFromTruncated(slot.name);
                float4 color = (Vector4)slot.color.linear;

                var writer = new BakerWriter(this, player);
                var stock = PlayerSetup.Add(ref writer, faction, playerName, color, populationCap: 0);
                AddStartingResources(stock, slot);
                var buildOrder = slot.buildOrder != null ? slot.buildOrder : authoring.defaultBuildOrder;

                if (slot.control == PlayerControl.AI)
                {
                    var tuning = authoring.AITuningFor(slot.difficulty);
                    AddBuildOrder(AIPlayerSetup.Add(ref writer, tuning.ToComponent()), buildOrder);
                    AddIncomeBonus(player, tuning.incomeMultiplier);
                    return;
                }

                if (slot.control == PlayerControl.LocalHuman)
                {
                    AddComponent<LocalPlayer>(player);
                }

                // Kept for a match setup that hands this slot to the AI.
                AddBuildOrder(writer.AddBuffer<AIBuildStep>(), buildOrder);
            }

            private void AddIncomeBonus(Entity player, float multiplier)
            {
                if (!Mathf.Approximately(multiplier, 1f))
                {
                    AddComponent(player, new IncomeMultiplier { Value = multiplier });
                }
            }

            private void AddBuildOrder(DynamicBuffer<AIBuildStep> steps, AIBuildOrder buildOrder)
            {
                if (buildOrder == null)
                {
                    return;
                }

                DependsOn(buildOrder);
                foreach (var step in buildOrder.steps)
                {
                    if (step.prefab != null)
                    {
                        // None: the prefab's own bakers pick its transform usage (upgrades have none).
                        var prefab = GetEntity(step.prefab, TransformUsageFlags.None);
                        steps.Add(new AIBuildStep { Prefab = prefab, Count = step.count });
                    }
                }
            }

            private static void AddStartingResources(DynamicBuffer<ResourceStock> stock, PlayerSlot slot)
            {
                foreach (var quantity in slot.startingResources)
                {
                    if (quantity.type != null)
                    {
                        ResourceMath.Add(stock, quantity.type, quantity.amount);
                    }
                }
            }
        }
    }
}
