using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Power
{
    /// <summary>
    /// Totals each player's <see cref="PowerGrid"/> from completed buildings and flags consumers
    /// <see cref="Unpowered"/> while the owner draws more than it produces. Runs before combat so weapons see it.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderLast = true)]
    public partial struct PowerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var produced = CollectionHelper.CreateNativeArray<float>(PlayerLookup.Capacity, state.WorldUpdateAllocator);
            var consumed = CollectionHelper.CreateNativeArray<float>(PlayerLookup.Capacity, state.WorldUpdateAllocator);

            new CountJob { Produced = produced, Consumed = consumed }.Schedule();
            new ApplyJob { Produced = produced, Consumed = consumed }.ScheduleParallel();
            new FlagJob { Produced = produced, Consumed = consumed }.ScheduleParallel();
        }

        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Dead))]
        private partial struct CountJob : IJobEntity
        {
            public NativeArray<float> Produced;
            public NativeArray<float> Consumed;

            private void Execute(in Faction faction, in PowerSupply supply)
            {
                Produced[faction.Value] += math.max(0f, supply.Amount);
                Consumed[faction.Value] += math.max(0f, -supply.Amount);
            }
        }

        [BurstCompile]
        private partial struct ApplyJob : IJobEntity
        {
            [ReadOnly] public NativeArray<float> Produced;
            [ReadOnly] public NativeArray<float> Consumed;

            private void Execute(ref PowerGrid grid, in Player player) =>
                grid = new PowerGrid { Produced = Produced[player.Faction], Consumed = Consumed[player.Faction] };
        }

        [BurstCompile]
        [WithPresent(typeof(Unpowered))]
        private partial struct FlagJob : IJobEntity
        {
            [ReadOnly] public NativeArray<float> Produced;
            [ReadOnly] public NativeArray<float> Consumed;

            private void Execute(in Faction faction, EnabledRefRW<Unpowered> unpowered)
            {
                var low = Consumed[faction.Value] > Produced[faction.Value];
                if (unpowered.ValueRO != low)
                {
                    unpowered.ValueRW = low;
                }
            }
        }
    }
}
