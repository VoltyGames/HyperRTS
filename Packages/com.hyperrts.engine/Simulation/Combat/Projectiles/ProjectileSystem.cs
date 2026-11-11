using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Projectiles
{
    /// <summary>
    /// Flies projectiles along their <see cref="ShotCourse"/> toward their aim point, queues their
    /// <see cref="DamageEvent"/> and impact <see cref="CueEvent"/> on arrival and destroys them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    public partial struct ProjectileSystem : ISystem
    {
        private TargetLookup _targets;
        private TargetQueries _targetQueries;
        private EventWriter<DamageEvent> _damage;
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            _damage = new EventWriter<DamageEvent>(ref state);
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);
            _damage.Update(ref state);
            _cues.Update(ref state);

            new HomingJob { Targets = _targets }.ScheduleParallel();
            new FlightJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Damage = _damage,
                Cues = _cues,
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
                    projectile.Track(Targets.Position(target), Targets.Radius(target));
                }
            }
        }

        // Single-threaded: every impact appends to the one damage queue.
        [BurstCompile]
        private partial struct FlightJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            public EventWriter<DamageEvent> Damage;
            public CueWriter Cues;

            private void Execute(Entity entity, ref LocalTransform transform, ref Projectile projectile)
            {
                if (projectile.Intercepted)
                {
                    return;
                }

                var arrived = projectile.Arcs ? FlyArc(ref transform, ref projectile) : FlyDirect(ref transform, projectile);
                if (!arrived)
                {
                    return;
                }

                var hit = projectile.Hit;
                hit.Origin = transform.Position;
                Damage.Add(hit);
                Ecb.DestroyEntity(entity);
                hit.AddImpact(ref Cues, projectile.ImpactTypeId, transform.Forward());
            }

            private bool FlyDirect(ref LocalTransform transform, in Projectile projectile)
            {
                var toTarget = projectile.Hit.Position - transform.Position;
                var distance = math.length(toTarget);
                var step = projectile.Speed * DeltaTime;
                if (distance <= step)
                {
                    return true;
                }

                transform.Position += toTarget / distance * step;
                transform.Rotation = quaternion.LookRotationSafe(toTarget, math.up());
                return false;
            }

            private bool FlyArc(ref LocalTransform transform, ref Projectile projectile)
            {
                projectile.Advance(DeltaTime);
                transform.Position = projectile.Position();
                transform.Rotation = quaternion.LookRotationSafe(projectile.Heading(), math.up());
                return projectile.Arrived;
            }
        }
    }
}
