using HyperRTS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Moves every entity that has a <see cref="MoveDestination"/> toward that
    /// destination at its <see cref="MovementSpeed"/>. Once the entity arrives
    /// within <see cref="ArriveThreshold"/> the destination is removed so the unit
    /// stops and the move order is cleared.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct MovementSystem : ISystem
    {
        private const float ArriveThreshold = 0.05f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, speed, destination, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<MovementSpeed>, RefRO<MoveDestination>>()
                         .WithEntityAccess())
            {
                var current = transform.ValueRO.Position;
                var toTarget = destination.ValueRO.Value - current;
                var distance = math.length(toTarget);

                if (distance <= ArriveThreshold)
                {
                    ecb.RemoveComponent<MoveDestination>(entity);
                    continue;
                }

                var direction = toTarget / distance;
                var step = math.min(distance, speed.ValueRO.Value * deltaTime);

                transform.ValueRW.Position = current + direction * step;
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up());
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
