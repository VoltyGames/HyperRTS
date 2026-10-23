using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Turns each <see cref="Interceptor"/>'s pose toward the nearest hostile projectile of a kind it engages, or
    /// failing one in range toward the nearest inbound on its radius, and, once on a target in range, engages it: a Fire
    /// cue toward it and a kill roll that marks it <see cref="Projectile.Intercepted"/>, destroys it and plays the
    /// Impact cue where it dies. Offline interceptors do nothing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    [UpdateBefore(typeof(ProjectileSystem))]
    public partial struct InterceptorSystem : ISystem
    {
        private EntityQuery _projectileQuery;
        private ComponentLookup<Projectile> _projectiles;
        private ComponentLookup<LocalTransform> _transforms;
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _projectileQuery = SystemAPI.QueryBuilder().WithAll<Projectile, LocalTransform>().Build();
            _projectiles = state.GetComponentLookup<Projectile>();
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<Interceptor>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _projectiles.Update(ref state);
            _transforms.Update(ref state);
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());

            // Single-threaded: two interceptors must never kill the same projectile.
            new InterceptJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                InFlight = _projectileQuery.ToEntityArray(state.WorldUpdateAllocator),
                Projectiles = _projectiles,
                Transforms = _transforms,
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Cues = _cues,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
        }

        [BurstCompile]
        [WithNone(typeof(Offline), typeof(Dead))]
        private partial struct InterceptJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public NativeArray<Entity> InFlight;
            public ComponentLookup<Projectile> Projectiles;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public FactionRelations Relations;
            public CueWriter Cues;
            public EntityCommandBuffer Ecb;

            private void Execute(Entity entity, ref Interceptor interceptor, DynamicBuffer<WeaponPose> poses,
                in Faction faction)
            {
                interceptor.Tick(DeltaTime);
                var place = Transforms[entity];
                interceptor.Target = Nearest(interceptor, place.Position, faction.Value, out var inRange);
                var pose = poses[interceptor.Pose];
                // Idle, it turns back to straight ahead and level.
                var angles = float2.zero;
                var tracking = interceptor.Target != Entity.Null;
                if (tracking)
                {
                    angles = interceptor.Aim.AimAngles(place, pose, Transforms[interceptor.Target].Position);
                }

                interceptor.Aim.Turn(ref pose, angles, DeltaTime);
                if (inRange && interceptor.Ready && interceptor.Aim.Aligned(pose, angles))
                {
                    pose.CountShot();
                    Engage(entity, ref interceptor, place, pose, faction.Value);
                }

                poses[interceptor.Pose] = pose;
            }

            // A shot aimed inside the radius is tracked before it arrives, so the turret is on it as it enters.
            private Entity Nearest(in Interceptor interceptor, float3 center, byte faction, out bool inRange)
            {
                var near = Closest.Within(interceptor.Radius);
                var inbound = Closest.None;
                for (var i = 0; i < InFlight.Length; i++)
                {
                    var shot = InFlight[i];
                    var projectile = Projectiles[shot];
                    if (!Engageable(interceptor, projectile, faction))
                    {
                        continue;
                    }

                    var distance = math.distance(Transforms[shot].Position, center);
                    near.Consider(i, shot, distance);
                    if (math.distance(projectile.Hit.Position, center) <= interceptor.Radius)
                    {
                        inbound.Consider(i, shot, distance);
                    }
                }

                inRange = near.Found;
                return inRange ? near.Entity : inbound.Entity;
            }

            private bool Engageable(in Interceptor interceptor, in Projectile projectile, byte faction)
            {
                if (projectile.Intercepted || !interceptor.Engages(projectile.Kind))
                {
                    return false;
                }

                return Relations.IsHostile(faction, projectile.Hit.SourceFaction);
            }

            private void Engage(Entity entity, ref Interceptor interceptor, in LocalTransform place,
                in WeaponPose pose, byte faction)
            {
                var target = interceptor.Target;
                var position = Transforms[target].Position;
                var muzzle = interceptor.Aim.LaunchPoint(place, pose);
                var toward = math.normalizesafe(position - muzzle, place.Forward());
                if (interceptor.Aim.HasMuzzle)
                {
                    Cues.PlayAt(entity, CueSlot.Fire, muzzle, toward, faction, interceptor.Pose);
                }
                else
                {
                    Cues.Play(entity, CueSlot.Fire, place, toward, faction, interceptor.Pose);
                }

                if (!interceptor.Engage(entity, target))
                {
                    return;
                }

                var projectile = Projectiles[target];
                projectile.Intercepted = true;
                Projectiles[target] = projectile;
                Ecb.DestroyEntity(target);
                Cues.Add(Cues.TypeIdFor(entity, CueSlot.Impact), CueSlot.Impact, position, toward, faction,
                    target: target);
            }
        }
    }
}
