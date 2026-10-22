using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
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
        private ComponentLookup<Faction> _factionLookup;
        private ComponentLookup<Dead> _deadLookup;
        private BufferLookup<ResourceStock> _stockLookup;
        private ComponentLookup<PlayerStats> _statsLookup;
        private BufferLookup<StatModifier> _modifierLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<AirdropCrate>();
            _factionLookup = state.GetComponentLookup<Faction>(true);
            _deadLookup = state.GetComponentLookup<Dead>(true);
            _stockLookup = state.GetBufferLookup<ResourceStock>();
            _statsLookup = state.GetComponentLookup<PlayerStats>();
            _modifierLookup = state.GetBufferLookup<StatModifier>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _factionLookup.Update(ref state);
            _deadLookup.Update(ref state);
            _stockLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _modifierLookup.Update(ref state);
            new LandJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                FactionLookup = _factionLookup,
                DeadLookup = _deadLookup,
                StockLookup = _stockLookup,
                StatsLookup = _statsLookup,
                ModifierLookup = _modifierLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded: several crates may pay into one player's stock.</summary>
        [BurstCompile]
        private partial struct LandJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            public PlayerRegistry Players;
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
                if (!FactionLookup.TryGetComponent(crate.Receiver, out var owner) || DeadLookup.HasEnabled(crate.Receiver))
                {
                    return;
                }

                if (owner.Value == Faction.Neutral)
                {
                    return;
                }

                var player = Players[owner.Value];
                var paid = ResourcePayout.Scaled(ModifierLookup, player, crate.Amount);
                ResourcePayout.Pay(ref StockLookup, ref StatsLookup, player, crate.Type, paid);
            }
        }
    }
}
