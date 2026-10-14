using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Totals each player's <see cref="PowerGrid"/> from completed buildings and sets
    /// <see cref="OfflineReason.Unpowered"/> on consumers while the owner draws more than it produces. Runs before
    /// combat so weapons see it.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderLast = true)]
    public partial struct PowerSystem : ISystem
    {
        private ComponentLookup<Offline> _offline;

        [BurstCompile]
        public void OnCreate(ref SystemState state) => _offline = state.GetComponentLookup<Offline>(true);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _offline.Update(ref state);
            var produced = CollectionHelper.CreateNativeArray<float>(PlayerRegistry.Capacity, state.WorldUpdateAllocator);
            var consumed = CollectionHelper.CreateNativeArray<float>(PlayerRegistry.Capacity, state.WorldUpdateAllocator);

            new CountJob { Produced = produced, Consumed = consumed, Offline = _offline }.Schedule();
            new ApplyJob { Produced = produced, Consumed = consumed }.ScheduleParallel();
            new FlagJob { Produced = produced, Consumed = consumed }.ScheduleParallel();
        }

        private static PowerGrid GetGrid(NativeArray<float> produced, NativeArray<float> consumed, byte faction) =>
            new() { Produced = produced[faction], Consumed = consumed[faction] };

        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Dead))]
        private partial struct CountJob : IJobEntity
        {
            public NativeArray<float> Produced;
            public NativeArray<float> Consumed;
            [ReadOnly] public ComponentLookup<Offline> Offline;

            // A building a game took offline (an EMP) neither supplies nor draws power.
            private void Execute(Entity entity, in Faction faction, in PowerSupply supply)
            {
                if (Offline.TryGetComponent(entity, out var offline) && offline.HasReasonOtherThan(OfflineReason.Engine))
                {
                    return;
                }

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
                grid = GetGrid(Produced, Consumed, player.Faction);
        }

        /// <summary>Sets the reason every frame, so a game overwriting <see cref="Offline"/> can't leave it stale.</summary>
        [BurstCompile]
        [WithPresent(typeof(Offline))]
        private partial struct FlagJob : IJobEntity
        {
            [ReadOnly] public NativeArray<float> Produced;
            [ReadOnly] public NativeArray<float> Consumed;

            private void Execute(in Faction faction, in PowerSupply supply, ref Offline offline)
            {
                if (supply.Amount >= 0f)
                {
                    return;
                }

                var low = GetGrid(Produced, Consumed, faction.Value).IsLow;
                offline.Set(OfflineReason.Unpowered, low);
            }
        }
    }
}
