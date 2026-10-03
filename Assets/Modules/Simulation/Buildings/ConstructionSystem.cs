using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Advances <see cref="ConstructionProgress"/> (a 0..1 fraction) on every building under
    /// construction, in a parallel Burst job. At full progress the component is disabled,
    /// marking the building complete without a structural change.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        // Fraction of construction completed per second (~10 seconds to finish).
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
