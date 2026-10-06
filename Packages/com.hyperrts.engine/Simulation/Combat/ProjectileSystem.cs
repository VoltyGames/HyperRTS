using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Flies projectiles toward their target (or its last known position once it dies), queues their
    /// <see cref="DamageEvent"/> on arrival and destroys them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    public partial struct ProjectileSystem : ISystem
    {
        /// <summary>Height above the shooter and target pivots that projectiles fly at.</summary>
        public const float FlightHeight = 1f;

        private TargetLookup _targets;
        private DamageWriter _damage;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _damage = new DamageWriter(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());

            new HomingJob { Targets = _targets }.ScheduleParallel();
            new FlightJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Damage = _damage,
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

        // Single-threaded: every impact appends to the one damage queue.
        [BurstCompile]
        private partial struct FlightJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            public DamageWriter Damage;

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

                var hit = projectile.Hit;
                hit.Position = projectile.TargetPosition;
                hit.Origin = transform.Position;
                Damage.Add(hit);
                Ecb.DestroyEntity(entity);
            }
        }
    }
}
