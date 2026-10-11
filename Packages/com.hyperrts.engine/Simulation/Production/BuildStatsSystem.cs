using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Counts units produced and buildings completed this frame into each owner's <see cref="PlayerStats"/>.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup), OrderLast = true)]
    public partial struct BuildStatsSystem : ISystem
    {
        private ComponentLookup<PlayerStats> _stats;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _stats = state.GetComponentLookup<PlayerStats>();
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stats.Update(ref state);
            new CountJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Stats = _stats,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct CountJob : IJobEntity
        {
            public PlayerRegistry Players;
            public ComponentLookup<PlayerStats> Stats;

            private void Execute(in DynamicBuffer<UnitProduced> units, in DynamicBuffer<BuildingCompleted> buildings)
            {
                foreach (var produced in units)
                {
                    if (Stats.TryGetRefRW(Players[produced.Faction], out var stats))
                    {
                        stats.ValueRW.UnitsBuilt++;
                    }
                }

                foreach (var completed in buildings)
                {
                    if (Stats.TryGetRefRW(Players[completed.Faction], out var stats))
                    {
                        stats.ValueRW.BuildingsBuilt++;
                    }
                }
            }
        }
    }
}
