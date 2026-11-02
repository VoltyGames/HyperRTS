using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Projectiles
{
    /// <summary>
    /// Turns the pose of each <see cref="Interceptor"/>'s mount toward the nearest hostile projectile of a kind it engages, or
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
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<Interceptor>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cues.Update(ref state);

            var candidates = new NativeList<Candidate>(state.WorldUpdateAllocator);
            new GatherJob { Candidates = candidates }.Schedule();
            // Single-threaded: two interceptors must never kill the same projectile.
            new InterceptJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Candidates = candidates,
                Projectiles = SystemAPI.GetComponentLookup<Projectile>(false),
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

            private void Execute(Entity entity, ref Interceptor interceptor, DynamicBuffer<WeaponMount> mounts,
                DynamicBuffer<WeaponPose> poses, in LocalTransform place, in Faction faction)
            {
                var mount = mounts[interceptor.Mount];
                mount.Weapon.Tick(DeltaTime);
                var inRange = false;
                var held = Deployable.Holds(Deployables, entity, mount.Weapon.NeedsDeploy);
                var target = held ? -1 : Nearest(interceptor, place.Position, faction.Value, out inRange);
                var pose = poses[interceptor.Mount];
                var aim = mount.Weapon.Aim;
                // Idle, it turns back to straight ahead and level.
                var angles = float2.zero;
                if (target >= 0)
                {
                    angles = aim.Angles(place, pose, Candidates[target].Position);
                }

                aim.Turn(ref pose, angles, DeltaTime);
                var ready = mount.Ready && !mount.Weapon.CoolingDown;
                if (inRange && ready && aim.Aligned(pose, angles))
                {
                    pose.CountShot();
                    Engage(new Firing(entity, faction.Value, interceptor.Mount, place, pose), ref interceptor, ref mount, target);
                }

                poses[interceptor.Mount] = pose;
                mounts[interceptor.Mount] = mount;
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

            private void Engage(in Firing firing, ref Interceptor interceptor, ref WeaponMount mount, int index)
            {
                var shot = Candidates[index];
                var aim = mount.Weapon.Aim;
                var toward = math.normalizesafe(shot.Position - aim.LaunchPoint(firing.Place, firing.Pose),
                    firing.Place.Forward());
                aim.PlayFire(ref Cues, firing, toward);
                if (!interceptor.Engage(ref mount, firing.Shooter, shot.Entity))
                {
                    return;
                }

                shot.Intercepted = true;
                Candidates[index] = shot;
                var projectile = Projectiles[shot.Entity];
                projectile.Intercepted = true;
                Projectiles[shot.Entity] = projectile;
                Ecb.DestroyEntity(shot.Entity);
                Cues.Add(new CueEvent
                {
                    TypeId = Cues.TypeIdFor(firing.Shooter, CueSlot.Impact), Slot = CueSlot.Impact,
                    Faction = firing.Faction, Position = shot.Position, Direction = toward, Target = shot.Entity,
                });
            }
        }
    }
}
