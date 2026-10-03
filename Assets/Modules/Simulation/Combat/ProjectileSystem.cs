using HyperRTS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Flies projectiles toward their target (or its last known position once it dies) and applies armor-scaled
    /// damage on arrival, then destroys the projectile.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    public partial struct ProjectileSystem : ISystem
    {
        /// <summary>Height above the shooter and target pivots that projectiles fly at.</summary>
        public const float FlightHeight = 1f;

        private TargetLookup _targets;
        private ComponentLookup<Health> _health;
        private BufferLookup<ArmorModifier> _armor;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _health = state.GetComponentLookup<Health>();
            _armor = state.GetBufferLookup<ArmorModifier>(true);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            _health.Update(ref state);
            _armor.Update(ref state);

            new HomingJob { Targets = _targets }.ScheduleParallel();
            new FlightJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Health = _health,
                Armor = _armor,
            }.Schedule();
        }

        [BurstCompile]
        private partial struct HomingJob : IJobEntity
        {
            public TargetLookup Targets;

            private void Execute(ref Projectile projectile)
            {
                if (Targets.IsAlive(projectile.Target))
                {
                    projectile.TargetPosition = Targets.Position(projectile.Target) + new float3(0f, FlightHeight, 0f);
                }
            }
        }

        // Single-threaded: several projectiles may land on the same target in one frame.
        [BurstCompile]
        private partial struct FlightJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            public ComponentLookup<Health> Health;
            [ReadOnly] public BufferLookup<ArmorModifier> Armor;

            private void Execute(Entity entity, ref LocalTransform transform, in Projectile projectile)
            {
                var toTarget = projectile.TargetPosition - transform.Position;
                var distance = math.length(toTarget);
                var step = projectile.Speed * DeltaTime;

                if (distance > step)
                {
                    transform.Position += toTarget / distance * step;
                    transform.Rotation = quaternion.LookRotationSafe(toTarget, math.up());
                    return;
                }

                CombatMath.ApplyDamage(ref Health, Armor, projectile.Target, projectile.Damage, projectile.DamageType);
                Ecb.DestroyEntity(entity);
            }
        }
    }
}
