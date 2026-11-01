using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Airdrops
{
    /// <summary>
    /// Sinks each <see cref="AirdropCrate"/> onto its receiver and, on landing, pays the receiver's owner scaled by
    /// <c>Stat.Income</c>. A crate whose receiver is gone lands for nothing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct AirdropCrateSystem : ISystem
    {

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<AirdropCrate>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new LandJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                ReceiverLookup = SystemAPI.GetComponentLookup<AirdropReceiver>(true),
                FactionLookup = SystemAPI.GetComponentLookup<Faction>(true),
                DeadLookup = SystemAPI.GetComponentLookup<Dead>(true),
                StockLookup = SystemAPI.GetBufferLookup<ResourceStock>(false),
                StatsLookup = SystemAPI.GetComponentLookup<PlayerStats>(false),
                ModifierLookup = SystemAPI.GetBufferLookup<StatModifier>(true),
            }.Schedule();
        }

        /// <summary>Single-threaded: several crates may pay into one player's stock.</summary>
        [BurstCompile]
        private partial struct LandJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            public PlayerRegistry Players;
            [ReadOnly] public ComponentLookup<AirdropReceiver> ReceiverLookup;
            [ReadOnly] public ComponentLookup<Faction> FactionLookup;
            [ReadOnly] public ComponentLookup<Dead> DeadLookup;
            public BufferLookup<ResourceStock> StockLookup;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public BufferLookup<StatModifier> ModifierLookup;

            private void Execute(Entity entity, ref LocalTransform transform, in AirdropCrate crate)
            {
                transform.Position.y -= crate.FallSpeed * DeltaTime;
                if (transform.Position.y > crate.Ground)
                {
                    return;
                }

                Ecb.DestroyEntity(entity);
                if (!ReceiverLookup.TryGetComponent(crate.Receiver, out var receiver)
                    || !FactionLookup.TryGetComponent(crate.Receiver, out var owner)
                    || DeadLookup.HasEnabled(crate.Receiver))
                {
                    return;
                }

                if (owner.Value == Faction.Neutral)
                {
                    return;
                }

                var player = Players[owner.Value];
                var paid = ResourcePayout.Scaled(ModifierLookup, player, receiver.Amount);
                ResourcePayout.Pay(ref StockLookup, ref StatsLookup, player, receiver.Type, paid);
            }
        }
    }
}
