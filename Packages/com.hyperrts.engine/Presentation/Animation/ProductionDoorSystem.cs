using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>Opens each <see cref="ProductionDoor"/> when its producer's release count rises, and poses and scales it.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PoseSystemGroup))]
    public partial struct ProductionDoorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<ProductionDoor>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new DoorJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Producers = SystemAPI.GetComponentLookup<Producer>(true),
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new StretchJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct DoorJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Producer> Producers;

            private void Execute(ref LocalTransform transform, ref ProductionDoor door)
            {
                if (!Producers.TryGetComponent(door.Building, out var producer))
                {
                    return;
                }

                door.Advance(producer.Released, DeltaTime);
                door.Pose(ref transform);
            }
        }

        /// <summary>Only doors baked with a scale pose carry a <see cref="PostTransformMatrix"/>.</summary>
        [BurstCompile]
        private partial struct StretchJob : IJobEntity
        {
            private void Execute(ref PostTransformMatrix stretch, in ProductionDoor door) =>
                stretch.Value = door.Stretch();
        }
    }
}
