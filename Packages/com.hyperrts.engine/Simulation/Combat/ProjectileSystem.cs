using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Flies projectiles toward their target (or its last known position once it dies), queues their
    /// <see cref="DamageEvent"/> and impact <see cref="CueEvent"/> on arrival and destroys them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    public partial struct ProjectileSystem : ISystem
    {
        private TargetLookup _targets;
        private EntityQuery _fogQuery;
        private DamageWriter _damage;
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _damage = new DamageWriter(ref state);
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _fogQuery);
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());

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
            public CueWriter Cues;

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
                Cues.AddImpact(projectile.ImpactTypeId, hit, transform.Forward());
            }
        }
    }
}
