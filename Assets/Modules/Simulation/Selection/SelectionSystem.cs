using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>
    /// Resolves the <see cref="SelectionInput"/> gesture into the live selection by toggling
    /// <see cref="Selected"/>. Click uses a physics raycast; drag-box and double-click project to screen.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct SelectionSystem : ISystem
    {
        private ComponentLookup<SelectableType> _typeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _typeLookup = state.GetComponentLookup<SelectableType>(true);
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

            // Click / double-click need the entity under the cursor (physics raycast).
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

            // Double-click selects every on-screen entity sharing the clicked entity's kind.
            var doubleClickKind = SelectableKind.None;
            if (input.Command == SelectionCommand.DoubleClick && clicked != Entity.Null &&
                _typeLookup.HasComponent(clicked))
            {
                doubleClickKind = _typeLookup[clicked].Kind;
            }

            foreach (var (transform, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>().WithAll<Selectable>().WithEntityAccess())
            {
                var hit = IsHit(in input, in _typeLookup, entity, transform.ValueRO.Position, clicked,
                    doubleClickKind);

                var current = SystemAPI.IsComponentEnabled<Selected>(entity);
                var next = SelectionMath.ResolveSelected(current, hit, input.Additive, input.Subtract);

                if (next != current)
                {
                    SystemAPI.SetComponentEnabled<Selected>(entity, next);
                }
            }
        }

        private static bool IsHit(in SelectionInput input, in ComponentLookup<SelectableType> typeLookup,
            Entity entity, float3 position, Entity clicked, SelectableKind doubleClickKind)
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
                    if (doubleClickKind == SelectableKind.None ||
                        !typeLookup.HasComponent(entity) ||
                        typeLookup[entity].Kind != doubleClickKind)
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
