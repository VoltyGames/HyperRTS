using HyperRTS.Core;
using HyperRTS.Simulation.Audio;
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
        private TargetLookup _targets;
        private DamageWriter _damage;
        private SoundWriter _sounds;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _damage = new DamageWriter(ref state);
            _sounds = new SoundWriter(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<SoundQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state);
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
            _sounds.Update(ref state, SystemAPI.GetSingletonEntity<SoundQueue>());

            new HomingJob { Targets = _targets }.ScheduleParallel();
            new FlightJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Damage = _damage,
                Sounds = _sounds,
            }.Schedule();
        }

        [BurstCompile]
        private partial struct HomingJob : IJobEntity
        {
            public TargetLookup Targets;

            private void Execute(ref Projectile projectile)
            {
                var target = projectile.Hit.Target;
                if (Targets.IsAlive(target))
                {
                    projectile.Hit.Position = Targets.Position(target) + new float3(0f, projectile.Height, 0f);
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
            public SoundWriter Sounds;

            private void Execute(Entity entity, ref LocalTransform transform, in Projectile projectile)
            {
                var toTarget = projectile.Hit.Position - transform.Position;
                var distance = math.length(toTarget);
                var step = projectile.Speed * DeltaTime;

                if (distance > step)
                {
                    transform.Position += toTarget / distance * step;
                    transform.Rotation = quaternion.LookRotationSafe(toTarget, math.up());
                    return;
                }

                var hit = projectile.Hit;
                hit.Origin = transform.Position;
                Damage.Add(hit);
                Ecb.DestroyEntity(entity);
                Sounds.Add(projectile.ImpactSoundTypeId, SoundSlot.Impact, transform.Position, hit.SourceFaction);
            }
        }
    }
}
