using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Vision.Detection;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Aims and fires every <see cref="WeaponMount"/> at its target, as a <see cref="DamageEvent"/>, a launched <see
    /// cref="Projectile"/> or an interceptor's kill roll. Offline, out-of-ammo and undeployed weapons stay silent.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponPoseSystem))]
    [UpdateBefore(typeof(StealthSystem))]
    public partial struct WeaponFireSystem : ISystem
    {
        private TargetQueries _targetQueries;
        private Shooter _shots;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
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
            _shots.Update(ref state, SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged), _targetQueries.Blockers);

            // Single-threaded: every shot appends to the one damage queue.
            new ShotJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Units = SystemAPI.GetComponentLookup<UnitTag>(true),
                Paces = SystemAPI.GetComponentLookup<Pace>(true),
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
            [ReadOnly] public ComponentLookup<Pace> Paces;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<FacingGoal> FacingGoals;
            public Shooter Shots;

            private void Execute(Entity entity, DynamicBuffer<WeaponMount> mounts, in Faction faction)
            {
                var rate = Paces.TryGetComponent(entity, out var pace) ? pace.FireRate : 1f;
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    mount.Weapon.Tick(DeltaTime * rate);
                    if (!CanShoot(entity, mount, rate))
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

            /// <summary>A target to shoot; a projectile only while it still flies, inside the weapon's range.</summary>
            private bool CanShoot(Entity entity, in WeaponMount mount, float rate)
            {
                if (mount.Target == Entity.Null || mount.Carriers == 0 || rate <= 0f)
                {
                    return false;
                }

                return !mount.Weapon.ShootsProjectiles || Shots.CanIntercept(entity, mount.Weapon, mount.Target);
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
