using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Applies the <see cref="SelectionInput"/> gesture by toggling <see cref="Selected"/>.</summary>
    // OrderFirst so selection settles before any command consumer in the order phase.
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct SelectionSystem : ISystem
    {
        private ComponentLookup<EntityInfo> _infoLookup;
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<UnitTag> _unitLookup;
        private ComponentLookup<BuildingTag> _buildingLookup;
        private ComponentLookup<ControlGroup> _groupLookup;
        private ComponentLookup<FogHidden> _hiddenLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _infoLookup = state.GetComponentLookup<EntityInfo>(true);
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _unitLookup = state.GetComponentLookup<UnitTag>(true);
            _buildingLookup = state.GetComponentLookup<BuildingTag>(true);
            _groupLookup = state.GetComponentLookup<ControlGroup>(true);
            _hiddenLookup = state.GetComponentLookup<FogHidden>(true);
            state.RequireForUpdate<SelectionInput>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            DeselectHidden(ref state);
            var input = SystemAPI.GetSingleton<SelectionInput>();
            if (input.Command == SelectionCommand.None)
            {
                return;
            }

            var test = CreateHitTest(ref state, in input);
            if (input.Command == SelectionCommand.AssignGroup)
            {
                AssignGroup(ref state, in test);
                return;
            }

            foreach (var (transform, selected, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, EnabledRefRW<Selected>>()
                         .WithAll<Selectable>()
                         .WithPresent<Selected>()
                         .WithEntityAccess())
            {
                var hit = test.IsHit(entity, transform.ValueRO.Position);
                var current = selected.ValueRO;
                var next = SelectionMath.ResolveSelected(current, hit, input.Additive, input.Subtract);

                if (next != current)
                {
                    selected.ValueRW = next;
                }
            }
        }

        private SelectionHitTest CreateHitTest(ref SystemState state, in SelectionInput input)
        {
            _hiddenLookup.Update(ref state);
            _infoLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _unitLookup.Update(ref state);
            _buildingLookup.Update(ref state);
            _groupLookup.Update(ref state);

            var test = new SelectionHitTest
            {
                Input = input,
                Clicked = Raycast(ref state, in input),
                LocalFaction = LocalFaction(ref state),
                PreferredRank = -1,
                Hidden = _hiddenLookup,
                Info = _infoLookup,
                Factions = _factionLookup,
                Units = _unitLookup,
                Buildings = _buildingLookup,
                Groups = _groupLookup,
            };

            // Double-click selects every on-screen entity of the clicked type and owner.
            if (input.Command == SelectionCommand.DoubleClick && _infoLookup.HasComponent(test.Clicked))
            {
                test.DoubleClickType = _infoLookup[test.Clicked].TypeId;
                test.DoubleClickFaction = test.FactionOf(test.Clicked);
            }

            // Ctrl-drag removes everything in the box, so only plain and Shift drags filter by rank.
            if (input.Command == SelectionCommand.DragRelease && !input.Subtract && test.LocalFaction >= 0)
            {
                test.PreferredRank = PreferredRank(ref state, in test);
            }

            return test;
        }

        private Entity Raycast(ref SystemState state, in SelectionInput input)
        {
            var needsRaycast = input.Command == SelectionCommand.Click ||
                               input.Command == SelectionCommand.DoubleClick;
            if (!needsRaycast || !SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
            {
                return Entity.Null;
            }

            var rayInput = new RaycastInput
            {
                Start = input.RayOrigin,
                End = input.RayOrigin + input.RayDirection * input.RayDistance,
                Filter = CollisionFilter.Default,
            };

            return physicsWorld.CastRay(rayInput, out var hit) && SystemAPI.HasComponent<Selectable>(hit.Entity)
                ? hit.Entity
                : Entity.Null;
        }

        // An enemy that walks into fog must not stay selected out of sight.
        private void DeselectHidden(ref SystemState state)
        {
            foreach (var selected in SystemAPI.Query<EnabledRefRW<Selected>>().WithAll<FogHidden>())
            {
                selected.ValueRW = false;
            }
        }

        private int LocalFaction(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<LocalPlayer>(out var local) && SystemAPI.HasComponent<Player>(local))
            {
                return SystemAPI.GetComponent<Player>(local).Faction;
            }

            return -1;
        }

        private int PreferredRank(ref SystemState state, in SelectionHitTest test)
        {
            var rank = 0;
            foreach (var (transform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Selectable>().WithEntityAccess())
            {
                if (test.InDragBox(transform.ValueRO.Position))
                {
                    rank = math.max(rank, test.Rank(entity));
                }
            }

            return rank;
        }

        private void AssignGroup(ref SystemState state, in SelectionHitTest test)
        {
            var bit = (byte)(1 << test.Input.Group);
            foreach (var (group, selected, entity) in
                     SystemAPI.Query<RefRW<ControlGroup>, EnabledRefRO<Selected>>()
                         .WithPresent<Selected>()
                         .WithEntityAccess())
            {
                var member = selected.ValueRO && test.CanJoinGroup(entity);
                group.ValueRW.Mask = (byte)(member ? group.ValueRO.Mask | bit : group.ValueRO.Mask & ~bit);
            }

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var newcomers = SystemAPI.QueryBuilder().WithAll<Selectable, Selected>().WithNone<ControlGroup>().Build()
                .ToEntityArray(Allocator.Temp);
            foreach (var entity in newcomers)
            {
                if (test.CanJoinGroup(entity))
                {
                    ecb.AddComponent(entity, new ControlGroup { Mask = bit });
                }
            }
        }
    }
}
