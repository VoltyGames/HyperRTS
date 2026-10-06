using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>Pays a dying entity's <see cref="ExperienceValue"/> to its last hostile attacker, if still alive.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct KillCreditSystem : ISystem
    {
        private ComponentLookup<Experience> _experience;
        private ComponentLookup<Health> _health;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _experience = state.GetComponentLookup<Experience>();
            _health = state.GetComponentLookup<Health>(true);
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _experience.Update(ref state);
            _health.Update(ref state);

            new CreditJob
            {
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Experience = _experience,
                Health = _health,
            }.Schedule();
        }

        /// <summary>Single-threaded: one killer may finish several victims in a frame.</summary>
        [BurstCompile]
        [WithAll(typeof(Dead))]
        private partial struct CreditJob : IJobEntity
        {
            public FactionRelations Relations;
            public ComponentLookup<Experience> Experience;
            [ReadOnly] public ComponentLookup<Health> Health;

            private void Execute(in LastAttacker attacker, in ExperienceValue value, in Faction faction)
            {
                var killer = attacker.Source;
                if (value.Value <= 0f || !Relations.IsHostile(attacker.Faction, faction.Value) ||
                    !Experience.HasComponent(killer) || !Health.TryGetComponent(killer, out var health) ||
                    health.Current <= 0f)
                {
                    return;
                }

                Experience.GetRefRW(killer).ValueRW.Points += value.Value;
            }
        }
    }
}
