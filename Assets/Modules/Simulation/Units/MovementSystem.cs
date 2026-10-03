using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Moves every entity with an enabled <see cref="MoveDestination"/> toward it at its
    /// <see cref="MovementSpeed"/>, in a parallel Burst job. On arrival (within
    /// <see cref="MoveJob.ArriveThreshold"/>) the order is disabled - no structural change.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MoveJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct MoveJob : IJobEntity
        {
            public const float ArriveThreshold = 0.05f;

            public float DeltaTime;

            // `ref` (not `in`) destination: the value and its EnabledRefRW must share one writable type handle.
            private void Execute(ref LocalTransform transform, in MovementSpeed speed, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> hasOrder)
            {
                var toTarget = destination.Value - transform.Position;
                var distance = math.length(toTarget);

                if (distance <= ArriveThreshold)
                {
                    hasOrder.ValueRW = false;
                    return;
                }

                var direction = toTarget / distance;
                transform.Position += direction * math.min(distance, speed.Value * DeltaTime);
                transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
            }
        }
    }
}
