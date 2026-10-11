using System;
using HyperRTS.Presentation.TeamColors;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Main-thread view of the match from the local player's side, shared by MonoBehaviour presenters.</summary>
    public sealed class MatchView
    {
        private static MatchView _default;

        private readonly Color[] _factionColors = new Color[byte.MaxValue + 1];
        private World _world;
        private int _frame = -1;
        private (int Players, int Override, uint Change) _colorsVersion = (-1, -1, 0u);
        private EntityQuery _localPlayer;
        private EntityQuery _players;
        private EntityQuery _colorOverride;
        private EntityQuery _relations;
        private EntityQuery _map;
        private EntityQuery _match;
        private EntityQuery _placement;
        private EntityQuery _pending;

        public EntityManager EntityManager { get; private set; }
        public bool IsReady { get; private set; }

        /// <summary>False once the bound world is disposed, e.g. when a network session swaps worlds mid-frame.</summary>
        public bool IsLive => _world != null && _world.IsCreated;
        public Entity LocalPlayer { get; private set; }
        public Player Local { get; private set; }
        public FactionRelations Relations { get; private set; }
        public bool HasMap { get; private set; }
        public MapSettings Map { get; private set; }

        /// <summary>The default world's view, refreshed once per frame however many presenters read it.</summary>
        public static bool TryGetDefault(out MatchView view)
        {
            _default ??= new MatchView();
            view = _default;
            var world = DefaultWorld.TryGet(out var live) ? live : null;
            var current = world == view._world && view._frame == Time.frameCount;
            if (!current)
            {
                view._frame = Time.frameCount;
                view.Refresh(world);
            }

            return view.IsReady;
        }

        /// <summary>Re-reads the singletons; false until <paramref name="world"/> has a local player (edit mode, loading).</summary>
        public bool Refresh(World world)
        {
            IsReady = false;
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

        public Relation RelationTo(byte faction) => Relations.RelationOf(Local.Faction, faction);

        /// <summary>Owner colour in sRGB, ready for UI and material property blocks.</summary>
        public Color ColorOf(byte faction) => _factionColors[faction];

        public bool TryGetMatch(out MatchState match) => _match.TryGetSingleton(out match);

        public bool IsLocalDefeated() => EntityManager.HasEnabled<Defeated>(LocalPlayer);

        /// <summary>
        /// Cancels a building placement or a targeted command waiting for a click; false when neither was pending.
        /// For a front end that owns Escape (<c>InputActionsProvider.FrontEndOwnsCancel</c>).
        /// </summary>
        public bool CancelInteraction()
        {
            var placing = _placement.TryGetSingleton(out PlacementState placement) && placement.Active;
            var targeting = _pending.TryGetSingleton(out PendingCommand pending) && pending.Type != CommandType.None;
            if (placing)
            {
                _placement.SetSingleton(new PlacementState());
            }

            if (targeting)
            {
                _pending.SetSingleton(new PendingCommand());
            }

            return placing || targeting;
        }

        private void Bind(World world)
        {
            _world = world;
            _colorsVersion = (-1, -1, 0u);
            EntityManager = world.EntityManager;
            _localPlayer = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalPlayer>(), ComponentType.ReadOnly<Player>());
            _players = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Player>());
            _colorOverride = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TeamColorOverride>());
            _relations = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>());
            _map = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MapSettings>());
            _match = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            _placement = EntityManager.CreateEntityQuery(ComponentType.ReadWrite<PlacementState>());
            _pending = EntityManager.CreateEntityQuery(ComponentType.ReadWrite<PendingCommand>());
        }

        private void RefreshColors()
        {
            var version = PlayersVersion();
            if (version == _colorsVersion)
            {
                return;
            }

            _colorsVersion = version;
            Array.Fill(_factionColors, Color.gray);
            _colorOverride.TryGetSingleton(out TeamColorOverride colorOverride);
            using var players = _players.ToComponentDataArray<Player>(Allocator.Temp);
            foreach (var player in players)
            {
                var color = colorOverride.TryGet(player.Faction, out var replaced) ? replaced : player.Color;
                _factionColors[player.Faction] = new Color(color.x, color.y, color.z, 1f).gamma;
            }
        }

        // Colours change only when players or the colour override are added, removed or rewritten.
        private (int Players, int Override, uint Change) PlayersVersion()
        {
            var change = Math.Max(LatestChange<Player>(_players), LatestChange<TeamColorOverride>(_colorOverride));
            return (EntityManager.GetComponentOrderVersion<Player>(),
                EntityManager.GetComponentOrderVersion<TeamColorOverride>(), change);
        }

        private uint LatestChange<T>(EntityQuery query) where T : unmanaged, IComponentData
        {
            var handle = EntityManager.GetComponentTypeHandle<T>(true);
            var change = 0u;
            using var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            foreach (var chunk in chunks)
            {
                change = Math.Max(change, chunk.GetChangeVersion(ref handle));
            }

            return change;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _default = null;
    }
}
