using System;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Main-thread view of the match from the local player's side, or an observer's when this world has no local
    /// player; shared by MonoBehaviour presenters.
    /// </summary>
    public sealed partial class MatchView
    {
        [AutoStaticsCleanup]
        private static MatchView _default;

        private readonly Color[] _factionColors = new Color[byte.MaxValue + 1];
        private World _world;
        private int _frame = -1;
        private EntityQuery _localPlayer;
        private EntityQuery _players;
        private readonly LiveSingleton<TeamColorOverride> _colorOverride = new();
        private readonly LiveSingleton<FactionRelations> _relations = new();
        private readonly LiveSingleton<MapSettings> _map = new();
        private readonly LiveSingleton<MatchState> _match = new();

        // Colours change only when players or the colour override are added, removed or rewritten.
        private readonly ChangeWatch _colors = new(() => new[]
        {
            ComponentType.ReadOnly<Player>(), ComponentType.ReadOnly<TeamColorOverride>(),
        });

        private readonly LiveSingleton<PlacementState> _placement = new();
        private readonly LiveSingleton<PendingCommand> _pending = new();
        private readonly LiveSingleton<CommandCardPage> _commandPage = new();

        public EntityManager EntityManager { get; private set; }
        public bool IsReady { get; private set; }

        /// <summary>False once the bound world is disposed, e.g. when a network session swaps worlds mid-frame.</summary>
        public bool IsLive => _world != null && _world.IsCreated;
        /// <summary><c>Entity.Null</c> for an observer.</summary>
        public Entity LocalPlayer { get; private set; }

        /// <summary>The local player's identity; default (neutral) for an observer.</summary>
        public Player Local { get; private set; }

        public bool HasLocalPlayer => LocalPlayer != Entity.Null;
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

        /// <summary>Re-reads the singletons; false until <paramref name="world"/> has a local player or a match.</summary>
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

            HasMap = _map.TryGet(EntityManager, out var map);
            Map = map;
            var hasMatch = _relations.TryGet(EntityManager, out var relations);
            Relations = relations;
            var hasLocal = _localPlayer.TryGetSingletonEntity<Player>(out var local);
            if (!hasLocal && !hasMatch)
            {
                return false;
            }

            LocalPlayer = hasLocal ? local : Entity.Null;
            Local = hasLocal ? EntityManager.GetComponentData<Player>(local) : default;
            RefreshColors();
            IsReady = true;
            return true;
        }

        public Relation RelationTo(byte faction) => Relations.GetRelation(Local.Faction, faction);

        /// <summary>Owner colour in sRGB, ready for UI and material property blocks.</summary>
        public Color GetColor(byte faction) => _factionColors[faction];

        /// <summary>The building being placed; inactive without the singleton.</summary>
        public PlacementState Placement => _placement.TryGet(EntityManager, out var placement) ? placement : default;

        /// <summary>The entity holding the placement and its segments.</summary>
        public Entity PlacementEntity => _placement.TryGetEntity(EntityManager, out var entity) ? entity : Entity.Null;

        public PendingCommand Pending => _pending.TryGet(EntityManager, out var pending) ? pending : default;

        /// <summary>The command card page that shows: 0 for the first, 1 for a builder's advanced options.</summary>
        public int CommandPage => _commandPage.TryGet(EntityManager, out var page) ? page.Page : 0;

        // The singletons come from ClientSingletonSystem, so a world without it (headless) ignores these writes.
        public void SetPlacement(in PlacementState placement) => Write(_placement, placement);

        public void SetPending(in PendingCommand pending) => Write(_pending, pending);

        public void SetCommandPage(int page) => Write(_commandPage, new CommandCardPage { Page = (byte)page });

        public bool TryGetMatch(out MatchState match) => _match.TryGet(EntityManager, out match);

        public bool IsLocalDefeated() => HasLocalPlayer && EntityManager.HasEnabled<Defeated>(LocalPlayer);

        /// <summary>
        /// Undoes the topmost of a building placement, a targeted command waiting for a click and the card's second page
        /// (see <see cref="InteractionState.Cancel"/>); false when there was nothing to undo.
        /// </summary>
        public bool CancelInteraction()
        {
            var state = new InteractionState
            {
                Placement = Placement, Pending = Pending, Page = new CommandCardPage { Page = (byte)CommandPage },
            };
            if (!state.Cancel())
            {
                return false;
            }

            SetPlacement(state.Placement);
            SetPending(state.Pending);
            SetCommandPage(state.Page.Page);
            return true;
        }

        private void Bind(World world)
        {
            _world = world;
            _colors.Forget();
            EntityManager = world.EntityManager;
            _localPlayer = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalPlayer>(), ComponentType.ReadOnly<Player>());
            _players = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Player>());
        }

        /// <summary>Sets the singleton while the view is live; a dead view drops the write.</summary>
        public void Write<T>(LiveSingleton<T> singleton, in T value) where T : unmanaged, IComponentData
        {
            if (IsLive)
            {
                singleton.Set(EntityManager, value);
            }
        }

        private void RefreshColors()
        {
            if (!_colors.Changed(EntityManager))
            {
                return;
            }

            Array.Fill(_factionColors, Color.gray);
            _colorOverride.TryGet(EntityManager, out var colorOverride);
            using var players = _players.ToComponentDataArray<Player>(Allocator.Temp);
            foreach (var player in players)
            {
                var color = colorOverride.TryGet(player.Faction, out var replaced) ? replaced : player.Color;
                _factionColors[player.Faction] = new Color(color.x, color.y, color.z, 1f).gamma;
            }
        }
    }
}
