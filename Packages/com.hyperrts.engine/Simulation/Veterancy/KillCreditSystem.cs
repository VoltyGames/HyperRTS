using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Veterancy
{
    /// <summary>
    /// Pays a dying entity's resource cost times <see cref="MatchRules.ExperiencePerCost"/> as experience to its last
    /// hostile attacker, if still alive.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct KillCreditSystem : ISystem
    {

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<MatchRules>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

            new CreditJob
            {
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                ExperiencePerCost = SystemAPI.GetSingleton<MatchRules>().ExperiencePerCost,
                Experience = SystemAPI.GetComponentLookup<Experience>(false),
                HealthLookup = SystemAPI.GetComponentLookup<Health>(true),
            }.Schedule();
        }

        /// <summary>Single-threaded: one killer may finish several victims in a frame.</summary>
        [BurstCompile]
        [WithAll(typeof(Dead))]
        private partial struct CreditJob : IJobEntity
        {
            public OwnerRelations Relations;
            public float ExperiencePerCost;
            public ComponentLookup<Experience> Experience;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;

            private void Execute(in LastAttacker attacker, in DynamicBuffer<ResourceCost> cost, in Owner owner)
            {
                var value = ExperiencePerCost * ResourceMath.Total(cost);
                if (value <= 0f || !Relations.IsHostile(attacker.Owner, owner.Value))
                {
                    return;
                }

                var killer = attacker.Source;
                if (!Experience.HasComponent(killer) || !Health.IsAlive(HealthLookup, killer))
                {
                    return;
                }

                Experience.GetRefRW(killer).ValueRW.Points += value;
            }
        }
    }
}
