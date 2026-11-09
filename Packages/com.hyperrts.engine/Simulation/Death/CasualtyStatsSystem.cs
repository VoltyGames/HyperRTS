using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Death
{
    /// <summary>
    /// Counts this frame's deaths in <see cref="PlayerStats"/>: a loss for the owner and a kill for the last attacker's
    /// player. Dead entities only exist for the frame they die in, so each is counted once.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct CasualtyStatsSystem : ISystem
    {
        private EntityQuery _dying;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerRegistry>();
            _dying = SystemAPI.QueryBuilder().WithAll<Dead, Owner>().WithAny<UnitTag, BuildingTag>().Build();
            state.RequireForUpdate<PlayerStats>();
            state.RequireForUpdate<OwnerRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_dying.IsEmpty)
            {
                return;
            }

            new CountJob
            {
                Players = SystemAPI.GetSingleton<PlayerRegistry>(),
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                StatsLookup = SystemAPI.GetComponentLookup<PlayerStats>(false),
                AttackerLookup = SystemAPI.GetComponentLookup<LastAttacker>(true),
                BuildingLookup = SystemAPI.GetComponentLookup<BuildingTag>(true),
            }.Schedule(_dying);
        }

        /// <summary>Single-threaded: many deaths can credit one player.</summary>
        [BurstCompile]
        [WithAll(typeof(Dead))]
        [WithAny(typeof(UnitTag), typeof(BuildingTag))]
        private partial struct CountJob : IJobEntity
        {
            public PlayerRegistry Players;
            public OwnerRelations Relations;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public ComponentLookup<LastAttacker> AttackerLookup;
            [ReadOnly] public ComponentLookup<BuildingTag> BuildingLookup;

            private void Execute(Entity entity, in Owner owner)
            {
                var building = BuildingLookup.HasComponent(entity);
                if (StatsLookup.TryGetRefRW(Players[owner.Value], out var ownerStats))
                {
                    ref var stats = ref ownerStats.ValueRW;
                    if (building)
                    {
                        stats.BuildingsLost++;
                    }
                    else
                    {
                        stats.UnitsLost++;
                    }
                }

                CreditKiller(entity, owner.Value, building);
            }

            private void CreditKiller(Entity entity, byte victim, bool building)
            {
                if (!AttackerLookup.TryGetComponent(entity, out var attacker) || !attacker.CreditsKill(Relations, victim))
                {
                    return;
                }

                if (!StatsLookup.TryGetRefRW(Players[attacker.Owner], out var killer))
                {
                    return;
                }

                ref var stats = ref killer.ValueRW;
                if (building)
                {
                    stats.BuildingsDestroyed++;
                }
                else
                {
                    stats.UnitsKilled++;
                }
            }
        }
    }
}
