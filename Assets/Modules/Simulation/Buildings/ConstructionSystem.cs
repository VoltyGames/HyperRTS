using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>Advances <see cref="ConstructionProgress"/> over the building's build time and disables it when complete.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new BuildJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(BuildingTag))]
        private partial struct BuildJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref ConstructionProgress progress, EnabledRefRW<ConstructionProgress> underConstruction,
                in Producible producible)
            {
                progress.Value += DeltaTime / math.max(producible.BuildTime, 0.01f);

                if (progress.Value >= 1f)
                {
                    progress.Value = 1f;
                    underConstruction.ValueRW = false;
                }
            }
        }
    }
}
