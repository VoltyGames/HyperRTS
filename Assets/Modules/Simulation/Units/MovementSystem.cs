using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Moves entities on the XZ plane toward an enabled <see cref="MoveDestination"/>; disables it on arrival.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct MovementSystem : ISystem
    {
        public const float ArriveThreshold = 0.05f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MoveJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct MoveJob : IJobEntity
        {
            public float DeltaTime;

            // `ref`, not `in`: it must share one writable handle with EnabledRefRW.
            private void Execute(ref LocalTransform transform, in MovementSpeed speed, ref MoveDestination destination,
                EnabledRefRW<MoveDestination> hasDestination)
            {
                var toTarget = destination.Value.xz - transform.Position.xz;
                var distance = math.length(toTarget);

                if (distance <= ArriveThreshold)
                {
                    hasDestination.ValueRW = false;
                    return;
                }

                var direction = toTarget / distance;
                transform.Position.xz += direction * math.min(distance, speed.Value * DeltaTime);
                transform.Rotation = quaternion.LookRotationSafe(new float3(direction.x, 0f, direction.y), math.up());
            }
        }
    }
}
