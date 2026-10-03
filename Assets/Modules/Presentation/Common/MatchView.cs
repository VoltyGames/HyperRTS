using System;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Main-thread view of the match from the local player's side, shared by MonoBehaviour presenters.</summary>
    public sealed class MatchView
    {
        private readonly Color[] _factionColors = new Color[byte.MaxValue + 1];
        private World _world;
        private EntityQuery _localPlayer;
        private EntityQuery _players;
        private EntityQuery _relations;
        private EntityQuery _map;
        private EntityQuery _match;

        public EntityManager EntityManager { get; private set; }
        public bool IsReady { get; private set; }
        public Entity LocalPlayer { get; private set; }
        public Player Local { get; private set; }
        public FactionRelations Relations { get; private set; }
        public bool HasMap { get; private set; }
        public MapSettings Map { get; private set; }

        /// <summary>Re-reads the singletons; false until a world with a local player exists (edit mode, loading).</summary>
        public bool Refresh()
        {
            IsReady = false;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (world != _world)
            {
                Bind(world);
            }

            HasMap = _map.TryGetSingleton(out MapSettings map);
            Map = map;
            _relations.TryGetSingleton(out FactionRelations relations);
            Relations = relations;
            if (!_localPlayer.TryGetSingletonEntity<Player>(out var local))
            {
                return false;
            }

            LocalPlayer = local;
            Local = EntityManager.GetComponentData<Player>(local);
            RefreshColors();
            IsReady = true;
            return true;
        }

        public Relation RelationTo(byte faction) => TeamRelation.Of(Local.Faction, faction, Relations);

        /// <summary>Owner colour in sRGB, ready for UI and material property blocks.</summary>
        public Color ColorOf(byte faction) => _factionColors[faction];

        public bool TryGetMatch(out MatchState match) => _match.TryGetSingleton(out match);

        public bool IsLocalDefeated() =>
            EntityManager.HasComponent<Defeated>(LocalPlayer) && EntityManager.IsComponentEnabled<Defeated>(LocalPlayer);

        private void Bind(World world)
        {
            _world = world;
            EntityManager = world.EntityManager;
            _localPlayer = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalPlayer>(), ComponentType.ReadOnly<Player>());
            _players = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Player>());
            _relations = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>());
            _map = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MapSettings>());
            _match = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
        }

        private void RefreshColors()
        {
            Array.Fill(_factionColors, Color.gray);
            using var players = _players.ToComponentDataArray<Player>(Allocator.Temp);
            foreach (var player in players)
            {
                var linear = new Color(player.Color.x, player.Color.y, player.Color.z, 1f);
                _factionColors[player.Faction] = linear.gamma;
            }
        }
    }
}
