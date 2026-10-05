using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
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
    public class MatchAuthoring : MonoBehaviour
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

        [Header("Players")]
        [Tooltip("Player slots. Slot 1 is faction 1, the 'Owner' number on units and buildings.")]
        public List<PlayerSetup> players = new()
        {
            new PlayerSetup { name = "Player", team = 1, color = PlayerSetup.Palette[0] },
            new PlayerSetup { name = "Enemy", team = 2, color = PlayerSetup.Palette[1], control = PlayerControl.AI },
        };

        [Header("AI")]
        [Tooltip("Seconds between AI decisions.")]
        [Min(0.1f)]
        public float aiThinkInterval = 2f;

        [Tooltip("Idle combat units the AI gathers before attacking.")]
        [Min(1)]
        public int aiAttackWaveSize = 6;

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
                });
                AddComponent(entity, new MatchState { Phase = MatchPhase.Playing });

                var relations = new FactionRelations();
                relations.Teams.Add(0);
                for (var i = 0; i < authoring.players.Count; i++)
                {
                    relations.Teams.Add((byte)authoring.players[i].team);
                    BakePlayer(authoring, authoring.players[i], (byte)(i + 1));
                }

                AddComponent(entity, relations);
            }

            private void BakePlayer(MatchAuthoring authoring, PlayerSetup setup, byte faction)
            {
                var player = CreateAdditionalEntity(TransformUsageFlags.None, entityName: setup.name);
                var playerName = new FixedString32Bytes();
                playerName.CopyFromTruncated(setup.name);
                float4 color = (Vector4)setup.color.linear;

                var sink = new BakerSink(this, player);
                var stock = PlayerSetup.Add(ref sink, faction, playerName, color, populationCap: 0);
                AddStartingResources(stock, setup);

                if (setup.control == PlayerControl.LocalHuman)
                {
                    AddComponent<LocalPlayer>(player);
                }
                else if (setup.control == PlayerControl.AI)
                {
                    AddComponent(player, new AIPlayer
                    {
                        ThinkInterval = authoring.aiThinkInterval,
                        TimeUntilThink = authoring.aiThinkInterval,
                        AttackWaveSize = authoring.aiAttackWaveSize,
                    });
                }
            }

            private static void AddStartingResources(DynamicBuffer<ResourceStock> stock, PlayerSetup setup)
            {
                foreach (var quantity in setup.startingResources)
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
