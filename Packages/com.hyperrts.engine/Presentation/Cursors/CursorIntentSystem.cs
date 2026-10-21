using HyperRTS.Client.Interaction;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Cursors
{
    /// <summary>
    /// Decides <see cref="CursorIntent"/> each frame: edge scrolling, then the pointer over UI, building placement, a
    /// pending targeted command, and last what a right-click with the selection would do, through the same
    /// <see cref="OrderResolver"/> the simulation uses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct CursorIntentSystem : ISystem
    {
        private OrderResolver _resolver;
        private EntityQuery _fogQuery;
        private EntityQuery _selected;
        private EntityQuery _localPlayer;
        private ComponentLookup<Inside> _inside;
        private ComponentLookup<Selectable> _selectable;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _resolver = new OrderResolver(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _selected = SystemAPI.QueryBuilder().WithAll<Selected, Faction>().WithNone<Dead>().Build();
            _localPlayer = SystemAPI.QueryBuilder().WithAll<LocalPlayer, Player>().Build();
            _inside = state.GetComponentLookup<Inside>(true);
            _selectable = state.GetComponentLookup<Selectable>(true);
            state.RequireForUpdate<CursorIntent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.SetSingleton(new CursorIntent { Kind = Decide(ref state) });
        }

        private CursorKind Decide(ref SystemState state)
        {
            var edge = SystemAPI.GetSingleton<ScrollEdge>().Direction;
            if (math.any(edge != 0))
            {
                return CursorIntent.Scroll(edge);
            }

            if (SystemAPI.GetSingleton<PointerState>().OverUI)
            {
                return CursorKind.Default;
            }

            var placement = SystemAPI.GetSingleton<PlacementState>();
            if (placement.Active)
            {
                return placement.Valid ? CursorKind.Build : CursorKind.Invalid;
            }

            var pending = SystemAPI.GetSingleton<PendingCommand>().Type;
            if (pending != CommandType.None)
            {
                return CursorIntent.ForPending(pending);
            }

            return RightClick(ref state, SystemAPI.GetSingleton<HoveredEntity>().Value);
        }

        /// <summary>The highest-precedence order a right-click on <paramref name="hovered"/> gives the selection.</summary>
        private CursorKind RightClick(ref SystemState state, Entity hovered)
        {
            _selectable.Update(ref state);
            var selectable = _selectable.HasComponent(hovered);
            if (_selected.IsEmpty || !_localPlayer.TryGetSingleton(out Player local) || !SystemAPI.HasSingleton<FactionRelations>())
            {
                return selectable ? CursorKind.Select : CursorKind.Default;
            }

            state.CompleteDependency();
            _resolver.Update(ref state, _fogQuery);
            _inside.Update(ref state);
            var relations = SystemAPI.GetSingleton<FactionRelations>();
            var best = CursorKind.Default;
            var producers = false;
            var moves = false;
            using var selected = _selected.ToEntityArray(Allocator.Temp);
            foreach (var entity in selected)
            {
                if (SystemAPI.GetComponent<Faction>(entity).Value != local.Faction)
                {
                    continue;
                }

                producers |= SystemAPI.HasComponent<Producer>(entity);
                if (!TakesOrders(ref state, entity))
                {
                    continue;
                }

                var order = _resolver.Resolve(CommandType.Smart, entity, hovered, relations).Type;
                moves |= order == OrderType.Move;
                var kind = CursorIntent.ForOrder(order);
                best = CursorIntent.Precedence(kind) > CursorIntent.Precedence(best) ? kind : best;
                if (CursorIntent.Precedence(best) == CursorIntent.HighestPrecedence)
                {
                    break;
                }
            }

            return Settle(best, producers, moves, selectable);
        }

        private bool TakesOrders(ref SystemState state, Entity entity)
        {
            if (!SystemAPI.HasComponent<ActiveOrder>(entity))
            {
                return false;
            }

            return !_inside.HasEnabled(entity);
        }

        // Clicking a selectable entity that no unit acts on selects it, and producers alone set their rally point.
        private static CursorKind Settle(CursorKind best, bool producers, bool moves, bool selectable)
        {
            var acts = best != CursorKind.Default;
            if (acts)
            {
                return best;
            }

            if (selectable)
            {
                return CursorKind.Select;
            }

            var setsRally = producers && !moves;
            return setsRally ? CursorKind.Rally : CursorKind.Default;
        }
    }
}
