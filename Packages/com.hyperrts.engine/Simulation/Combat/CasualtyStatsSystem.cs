using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat
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
        private EntityQuery _players;
        private ComponentLookup<PlayerStats> _statsLookup;
        private ComponentLookup<LastAttacker> _attackerLookup;
        private ComponentLookup<BuildingTag> _buildingLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _players = SystemAPI.QueryBuilder().WithAll<Player>().Build();
            _statsLookup = state.GetComponentLookup<PlayerStats>();
            _attackerLookup = state.GetComponentLookup<LastAttacker>(true);
            _buildingLookup = state.GetComponentLookup<BuildingTag>(true);
            state.RequireForUpdate<PlayerStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _statsLookup.Update(ref state);
            _attackerLookup.Update(ref state);
            _buildingLookup.Update(ref state);
            new CountJob
            {
                PlayerByFaction = PlayerLookup.ByFaction(_players, state.WorldUpdateAllocator),
                StatsLookup = _statsLookup,
                AttackerLookup = _attackerLookup,
                BuildingLookup = _buildingLookup,
            }.Schedule();
        }

        /// <summary>Single-threaded: many deaths can credit one player.</summary>
        [BurstCompile]
        [WithAll(typeof(Dead))]
        [WithAny(typeof(UnitTag), typeof(BuildingTag))]
        private partial struct CountJob : IJobEntity
        {
            [ReadOnly] public NativeArray<Entity> PlayerByFaction;
            public ComponentLookup<PlayerStats> StatsLookup;
            [ReadOnly] public ComponentLookup<LastAttacker> AttackerLookup;
            [ReadOnly] public ComponentLookup<BuildingTag> BuildingLookup;

            private void Execute(Entity entity, in Faction faction)
            {
                var building = BuildingLookup.HasComponent(entity);
                if (StatsLookup.TryGetRefRW(PlayerByFaction[faction.Value], out var owner))
                {
                    ref var stats = ref owner.ValueRW;
                    if (building)
                    {
                        stats.BuildingsLost++;
                    }
                    else
                    {
                        stats.UnitsLost++;
                    }
                }

                CreditKiller(entity, faction.Value, building);
            }

            private void CreditKiller(Entity entity, byte victim, bool building)
            {
                if (!AttackerLookup.TryGetComponent(entity, out var attacker) || attacker.Faction == victim)
                {
                    return;
                }

                if (!StatsLookup.TryGetRefRW(PlayerByFaction[attacker.Faction], out var killer))
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
