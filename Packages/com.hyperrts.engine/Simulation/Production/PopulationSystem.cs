using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Recounts each player's <see cref="Population"/> from living units and completed providers.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup), OrderFirst = true)]
    public partial struct PopulationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var used = CollectionHelper.CreateNativeArray<int>(PlayerLookup.Capacity, state.WorldUpdateAllocator);
            var cap = CollectionHelper.CreateNativeArray<int>(PlayerLookup.Capacity, state.WorldUpdateAllocator);

            new CountUsedJob { Used = used }.Schedule();
            new CountCapJob { Cap = cap }.Schedule();
            new ApplyJob { Used = used, Cap = cap }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(UnitTag))]
        [WithNone(typeof(Dead))]
        private partial struct CountUsedJob : IJobEntity
        {
            public NativeArray<int> Used;

            private void Execute(in Faction faction, in Producible producible) =>
                Used[faction.Value] += producible.Population;
        }

        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Dead))]
        private partial struct CountCapJob : IJobEntity
        {
            public NativeArray<int> Cap;

            private void Execute(in Faction faction, in PopulationProvider provider) =>
                Cap[faction.Value] += provider.Value;
        }

        [BurstCompile]
        private partial struct ApplyJob : IJobEntity
        {
            [ReadOnly] public NativeArray<int> Used;
            [ReadOnly] public NativeArray<int> Cap;

            private void Execute(ref Population population, in Player player) =>
                population = new Population { Used = Used[player.Faction], Cap = Cap[player.Faction] };
        }
    }
}
