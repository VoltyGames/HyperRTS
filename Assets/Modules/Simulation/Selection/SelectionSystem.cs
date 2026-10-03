using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Applies the <see cref="SelectionInput"/> gesture by toggling <see cref="Selected"/>.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct SelectionSystem : ISystem
    {
        private ComponentLookup<EntityInfo> _typeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _typeLookup = state.GetComponentLookup<EntityInfo>(true);
            state.RequireForUpdate<SelectionInput>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var input = SystemAPI.GetSingleton<SelectionInput>();
            if (input.Command == SelectionCommand.None)
            {
                return;
            }

            _typeLookup.Update(ref state);

            var clicked = Entity.Null;
            var needsRaycast = input.Command == SelectionCommand.Click ||
                               input.Command == SelectionCommand.DoubleClick;
            if (needsRaycast && SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
            {
                var rayInput = new RaycastInput
                {
                    Start = input.RayOrigin,
                    End = input.RayOrigin + input.RayDirection * input.RayDistance,
                    Filter = CollisionFilter.Default,
                };

                if (physicsWorld.CastRay(rayInput, out var hit) && SystemAPI.HasComponent<Selectable>(hit.Entity))
                {
                    clicked = hit.Entity;
                }
            }

            // Double-click selects every on-screen entity of the clicked type.
            var doubleClickType = 0;
            if (input.Command == SelectionCommand.DoubleClick && clicked != Entity.Null &&
                _typeLookup.HasComponent(clicked))
            {
                doubleClickType = _typeLookup[clicked].TypeId;
            }

            // WithPresent also visits unselected entities.
            foreach (var (transform, selected, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>, EnabledRefRW<Selected>>()
                         .WithAll<Selectable>()
                         .WithPresent<Selected>()
                         .WithEntityAccess())
            {
                var hit = IsHit(in input, in _typeLookup, entity, transform.ValueRO.Position, clicked,
                    doubleClickType);

                var current = selected.ValueRO;
                var next = SelectionMath.ResolveSelected(current, hit, input.Additive, input.Subtract);

                if (next != current)
                {
                    selected.ValueRW = next;
                }
            }
        }

        private static bool IsHit(in SelectionInput input, in ComponentLookup<EntityInfo> typeLookup,
            Entity entity, float3 position, Entity clicked, int doubleClickType)
        {
            switch (input.Command)
            {
                case SelectionCommand.Click:
                    return entity == clicked;

                case SelectionCommand.DragRelease:
                    return SelectionMath.WorldToScreenPoint(input.ViewProjection, position, input.ScreenSize,
                               out var dragScreen) &&
                           SelectionMath.RectContains(input.DragMin, input.DragMax, dragScreen);

                case SelectionCommand.DoubleClick:
                    if (doubleClickType == 0 ||
                        !typeLookup.HasComponent(entity) ||
                        typeLookup[entity].TypeId != doubleClickType)
                    {
                        return false;
                    }

                    return SelectionMath.WorldToScreenPoint(input.ViewProjection, position, input.ScreenSize,
                               out var typeScreen) &&
                           SelectionMath.RectContains(float2.zero, input.ScreenSize, typeScreen);

                default:
                    return false;
            }
        }
    }
}
