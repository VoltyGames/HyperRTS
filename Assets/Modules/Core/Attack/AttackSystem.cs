using HyperRTS.Core.Health;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Core.Attack
{
    /// <summary>
    /// Applies damage from attackers to their <see cref="AttackTarget"/> on a fixed
    /// cadence driven by <see cref="AttackCooldown"/>. An attacker is expected to
    /// carry exactly one damage component (<see cref="MeleeAttackDamage"/> or
    /// <see cref="RangeAttackDamage"/>); the target's <see cref="HealthComponent"/>
    /// is reduced in place via a component lookup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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

            foreach (var (target, damage, cooldown) in
                     SystemAPI.Query<RefRO<AttackTarget>, RefRO<MeleeAttackDamage>, RefRW<AttackCooldown>>())
            {
                ApplyDamage(ref _healthLookup, target.ValueRO.Value, damage.ValueRO.Value,
                    ref cooldown.ValueRW, deltaTime);
            }

            foreach (var (target, damage, cooldown) in
                     SystemAPI.Query<RefRO<AttackTarget>, RefRO<RangeAttackDamage>, RefRW<AttackCooldown>>())
            {
                ApplyDamage(ref _healthLookup, target.ValueRO.Value, damage.ValueRO.Value,
                    ref cooldown.ValueRW, deltaTime);
            }
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

            if (!healthLookup.HasComponent(target))
            {
                return;
            }

            var health = healthLookup[target];
            health.CurrentHealth -= damage;
            healthLookup[target] = health;
        }
    }
}
