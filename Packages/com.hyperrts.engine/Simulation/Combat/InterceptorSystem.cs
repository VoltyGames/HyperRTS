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
    /// Impact cue where it dies. Offline interceptors, and those waiting for their unit to deploy, do nothing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    [UpdateBefore(typeof(ProjectileSystem))]
    public partial struct InterceptorSystem : ISystem
    {
        private ComponentLookup<Projectile> _projectiles;
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _projectiles = state.GetComponentLookup<Projectile>();
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
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());

            var candidates = new NativeList<Candidate>(state.WorldUpdateAllocator);
            new GatherJob { Candidates = candidates }.Schedule();
            // Single-threaded: two interceptors must never kill the same projectile.
            new InterceptJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Candidates = candidates,
                Projectiles = _projectiles,
                Deployables = SystemAPI.GetComponentLookup<Deployable>(true),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Cues = _cues,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule();
        }

        /// <summary>A projectile some interceptor could engage, copied once so each interceptor scans a compact list.</summary>
        private struct Candidate
        {
            public Entity Entity;
            public float3 Position;
            public float3 Aim;
            public ProjectileKind Kind;
            public byte Faction;
            public bool Intercepted;
        }

        [BurstCompile]
        private partial struct GatherJob : IJobEntity
        {
            public NativeList<Candidate> Candidates;

            private void Execute(Entity entity, in Projectile projectile, in LocalTransform transform)
            {
                if (projectile.Kind == ProjectileKind.None || projectile.Intercepted)
                {
                    return;
                }

                Candidates.Add(new Candidate
                {
                    Entity = entity,
                    Position = transform.Position,
                    Aim = projectile.Hit.Position,
                    Kind = projectile.Kind,
                    Faction = projectile.Hit.SourceFaction,
                });
            }
        }

        [BurstCompile]
        [WithNone(typeof(Offline), typeof(Dead))]
        private partial struct InterceptJob : IJobEntity
        {
            public float DeltaTime;
            public NativeList<Candidate> Candidates;
            public ComponentLookup<Projectile> Projectiles;
            [ReadOnly] public ComponentLookup<Deployable> Deployables;
            public FactionRelations Relations;
            public CueWriter Cues;
            public EntityCommandBuffer Ecb;

            private void Execute(Entity entity, ref Interceptor interceptor, DynamicBuffer<WeaponPose> poses,
                in LocalTransform place, in Faction faction)
            {
                interceptor.Tick(DeltaTime);
                var inRange = false;
                var held = Deployable.Holds(Deployables, entity, interceptor.Aim);
                var target = held ? -1 : Nearest(interceptor, place.Position, faction.Value, out inRange);
                var pose = poses[interceptor.Pose];
                // Idle, it turns back to straight ahead and level.
                var angles = float2.zero;
                if (target >= 0)
                {
                    angles = interceptor.Aim.AimAngles(place, pose, Candidates[target].Position);
                }

                interceptor.Aim.Turn(ref pose, angles, DeltaTime);
                if (inRange && interceptor.Ready && interceptor.Aim.Aligned(pose, angles))
                {
                    pose.CountShot();
                    Engage(entity, ref interceptor, place, pose, faction.Value, target);
                }

                poses[interceptor.Pose] = pose;
            }

            // A shot aimed inside the radius is tracked before it arrives, so the turret is on it as it enters.
            private int Nearest(in Interceptor interceptor, float3 center, byte faction, out bool inRange)
            {
                var radiusSq = interceptor.Radius * interceptor.Radius;
                var near = Closest.Within(radiusSq);
                var inbound = Closest.None;
                for (var i = 0; i < Candidates.Length; i++)
                {
                    var shot = Candidates[i];
                    if (!Engageable(interceptor, shot, faction))
                    {
                        continue;
                    }

                    var distanceSq = math.distancesq(shot.Position, center);
                    near.Consider(i, shot.Entity, distanceSq);
                    if (math.distancesq(shot.Aim, center) <= radiusSq)
                    {
                        inbound.Consider(i, shot.Entity, distanceSq);
                    }
                }

                inRange = near.Found;
                return inRange ? near.Index : inbound.Index;
            }

            private bool Engageable(in Interceptor interceptor, in Candidate shot, byte faction)
            {
                if (shot.Intercepted || !interceptor.Engages(shot.Kind))
                {
                    return false;
                }

                return Relations.IsHostile(faction, shot.Faction);
            }

            private void Engage(Entity entity, ref Interceptor interceptor, in LocalTransform place,
                in WeaponPose pose, byte faction, int index)
            {
                var shot = Candidates[index];
                var toward = math.normalizesafe(shot.Position - interceptor.Aim.LaunchPoint(place, pose), place.Forward());
                interceptor.Aim.PlayFire(ref Cues, entity, place, pose, toward, faction, interceptor.Pose);
                if (!interceptor.Engage(entity, shot.Entity))
                {
                    return;
                }

                shot.Intercepted = true;
                Candidates[index] = shot;
                var projectile = Projectiles[shot.Entity];
                projectile.Intercepted = true;
                Projectiles[shot.Entity] = projectile;
                Ecb.DestroyEntity(shot.Entity);
                Cues.Add(Cues.TypeIdFor(entity, CueSlot.Impact), CueSlot.Impact, shot.Position, toward, faction,
                    target: shot.Entity);
            }
        }
    }
}
