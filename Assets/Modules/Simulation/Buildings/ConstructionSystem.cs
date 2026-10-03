using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Advances <see cref="ConstructionProgress"/> and disables it when complete.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        // ~10 s to build.
        public const float BuildRatePerSecond = 0.1f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new BuildJob { Step = BuildRatePerSecond * SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(BuildingTag))]
        private partial struct BuildJob : IJobEntity
        {
            public float Step;

            private void Execute(ref ConstructionProgress progress, EnabledRefRW<ConstructionProgress> underConstruction)
            {
                progress.Value += Step;

                if (progress.Value >= 1f)
                {
                    progress.Value = 1f;
                    underConstruction.ValueRW = false;
                }
            }
        }
    }
}
