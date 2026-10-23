using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Production;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Opens each <see cref="ProductionDoor"/> when its producer's release count rises or while its aircraft's
    /// <see cref="DropRun"/> is active, and poses and scales it.
    /// </summary>
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
                DropRuns = SystemAPI.GetComponentLookup<DropRun>(true),
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new StretchJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct DoorJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Producer> Producers;
            [ReadOnly] public ComponentLookup<DropRun> DropRuns;

            private void Execute(ref LocalTransform transform, ref ProductionDoor door)
            {
                if (Producers.TryGetComponent(door.Root, out var producer))
                {
                    door.Advance(producer.Released, DeltaTime);
                }
                else if (DropRuns.TryGetComponent(door.Root, out var run))
                {
                    door.Hold(run.Active, DeltaTime);
                }
                else
                {
                    return;
                }

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
