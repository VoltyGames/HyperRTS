using HyperRTS.Core;
using HyperRTS.Simulation.Health;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Attack
{
    /// <summary>
    /// Applies damage from attackers to their <see cref="AttackTarget"/> on a cadence driven by
    /// <see cref="AttackCooldown"/>. An attacker carries one damage component
    /// (<see cref="MeleeAttackDamage"/> or <see cref="RangeAttackDamage"/>). Burst jobs write the
    /// target's <see cref="HealthComponent"/> through a lookup, so they run single-threaded
    /// (several attackers can hit the same target).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct AttackSystem : ISystem
    {
        private ComponentLookup<HealthComponent> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _healthLookup = state.GetComponentLookup<HealthComponent>();
            state.RequireForUpdate<HealthComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            _healthLookup.Update(ref state);

            new MeleeAttackJob { HealthLookup = _healthLookup, DeltaTime = deltaTime }.Schedule();
            new RangeAttackJob { HealthLookup = _healthLookup, DeltaTime = deltaTime }.Schedule();
        }

        [BurstCompile]
        private partial struct MeleeAttackJob : IJobEntity
        {
            public ComponentLookup<HealthComponent> HealthLookup;
            public float DeltaTime;

            private void Execute(in AttackTarget target, in MeleeAttackDamage damage, ref AttackCooldown cooldown) =>
                ApplyDamage(ref HealthLookup, target.Value, damage.Value, ref cooldown, DeltaTime);
        }

        [BurstCompile]
        private partial struct RangeAttackJob : IJobEntity
        {
            public ComponentLookup<HealthComponent> HealthLookup;
            public float DeltaTime;

            private void Execute(in AttackTarget target, in RangeAttackDamage damage, ref AttackCooldown cooldown) =>
                ApplyDamage(ref HealthLookup, target.Value, damage.Value, ref cooldown, DeltaTime);
        }

        private static void ApplyDamage(ref ComponentLookup<HealthComponent> healthLookup, Entity target,
            int damage, ref AttackCooldown cooldown, float deltaTime)
        {
            cooldown.TimeRemaining -= deltaTime;

            if (cooldown.TimeRemaining > 0f)
            {
                return;
            }

            cooldown.TimeRemaining = cooldown.Interval;

            // A destroyed target simply has no health left to hit.
            if (healthLookup.TryGetRefRW(target, out var health))
            {
                health.ValueRW.CurrentHealth -= damage;
            }
        }
    }
}
