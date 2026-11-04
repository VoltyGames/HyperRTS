using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Aims and fires every <see cref="WeaponMount"/>: an instant <see cref="DamageEvent"/>, or a launched
    /// <see cref="Projectile"/> when the weapon has a prefab. The primary shoots the <see cref="AttackTarget"/> once it
    /// is in reach; an extra weapon shoots it when it can hit it, else, if independent, the nearest spotted hostile in
    /// its own range. Offline shooters, weapons out of <see cref="Ammo"/> and undeployed
    /// <see cref="Weapon.NeedsDeploy"/> weapons stay silent. A salvo stops when its target is lost, and a shot reveals
    /// a stealthed shooter for its <see cref="Stealth.RevealDuration"/>. A unit's hull-fixed primary turns the hull to
    /// the target through movement and fires once it faces it; a turret fires once its pose is on target.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(StealthSystem))]
    public partial struct WeaponFireSystem : ISystem
    {
        private TargetLookup _targets;
        private TargetQueries _targetQueries;
        private Shooter _shots;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _targetQueries = TargetQueries.Create(ref state);
            _shots = new Shooter(ref state);
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<FogOfWar>();
            state.RequireForUpdate<SimulationFrame>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _targetQueries);

            new WeaponTargetJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Targets = _targets,
                Scans = new ScanSchedule(SystemAPI.GetSingleton<SimulationFrame>()),
            }.ScheduleParallel();

            _shots.Update(ref state, SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged), _targetQueries.Blockers);

            // Single-threaded: every shot appends to the one damage queue.
            new ShotJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Units = SystemAPI.GetComponentLookup<UnitTag>(true),
                InsideLookup = SystemAPI.GetComponentLookup<Inside>(true),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                FacingGoals = SystemAPI.GetComponentLookup<FacingGoal>(false),
                Shots = _shots,
            }.Schedule();
        }

        [BurstCompile]
        [WithNone(typeof(Offline))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct ShotJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<UnitTag> Units;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<FacingGoal> FacingGoals;
            public Shooter Shots;

            private void Execute(Entity entity, DynamicBuffer<WeaponMount> mounts, in Faction faction)
            {
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    if (mount.Intercepts)
                    {
                        continue;
                    }

                    mount.Weapon.Tick(DeltaTime);
                    if (mount.Target == Entity.Null || mount.Carriers == 0)
                    {
                        mount.Weapon.EndSalvo();
                    }
                    else if (Faces(entity, mount) && Shots.TryFire(entity, ref mount.Weapon, (byte)i, mount.Target,
                                 faction.Value, mount.Carriers))
                    {
                        mount.Ammo.Spend();
                    }

                    mounts[i] = mount;
                }
            }

            /// <summary>A unit's hull-fixed weapon turns the hull; a garrisoned passenger fires out of any side without turning.</summary>
            private bool Faces(Entity entity, in WeaponMount mount)
            {
                var turnsHull = Units.HasComponent(entity) && !mount.Weapon.Aim.Turreted;
                if (!turnsHull || InsideLookup.HasEnabled(entity))
                {
                    return true;
                }

                return Face(entity, Transforms[mount.Target].Position, mount.Weapon.Aim.AimTolerance);
            }

            /// <summary>Turns the hull level toward the target; true once it faces it within <paramref name="tolerance"/>.</summary>
            private bool Face(Entity shooter, float3 targetPosition, float tolerance)
            {
                var transform = Transforms[shooter];
                var direction = math.normalizesafe((targetPosition - transform.Position).xz);
                // A target straight overhead lies on every heading.
                if (direction.Equals(float2.zero))
                {
                    return true;
                }

                FacingGoals[shooter] = new FacingGoal { Direction = direction };
                FacingGoals.SetComponentEnabled(shooter, true);
                var forward = math.normalizesafe(math.mul(transform.Rotation, math.forward()).xz);
                return math.dot(forward, direction) >= math.cos(tolerance);
            }
        }
    }
}
