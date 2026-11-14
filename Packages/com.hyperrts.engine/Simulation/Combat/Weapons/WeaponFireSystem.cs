using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
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
            state.RequireForUpdate<OwnerRelations>();
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
                PointAims = SystemAPI.GetComponentLookup<PointAim>(true),
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
            [ReadOnly] public ComponentLookup<PointAim> PointAims;
            public ComponentLookup<FacingGoal> FacingGoals;
            public Shooter Shots;

            private void Execute(Entity entity, DynamicBuffer<WeaponMount> mounts, in Owner owner)
            {
                var rate = Paces.TryGetComponent(entity, out var pace) ? pace.FireRate : 1f;
                var ordered = PointAims.HasEnabled(entity);
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    mount.Weapon.Tick(DeltaTime * rate);
                    var aim = ShotAim.Choose(mount.Target, Transforms, mount.Weapon, ordered && i == 0,
                        ordered ? PointAims[entity].Value : default);
                    if (!CanShoot(entity, mount, aim, rate))
                    {
                        mount.Weapon.EndSalvo();
                    }
                    else
                    {
                        mount.Weapon.SalvoAim = aim.Point;
                        if (Faces(entity, mount, aim) && Shots.TryFire(entity, ref mount.Weapon, (byte)i, aim,
                                owner.Value, mount.Carriers))
                        {
                            mount.Ammo.Spend();
                        }
                    }

                    mounts[i] = mount;
                }
            }

            /// <summary>An aim to shoot at; a projectile only while it still flies inside the weapon's range, a ground point only with rounds left.</summary>
            private bool CanShoot(Entity entity, in WeaponMount mount, in ShotAim aim, float rate)
            {
                if (!aim.Exists || mount.Carriers == 0 || rate <= 0f)
                {
                    return false;
                }

                if (mount.Weapon.ShootsProjectiles)
                {
                    return Shots.CanIntercept(entity, mount.Weapon, aim.Target);
                }

                return aim.Target != Entity.Null || mount.Ready;
            }

            /// <summary>A unit's hull-fixed weapon turns the hull; a garrisoned passenger fires out of any side without turning.</summary>
            private bool Faces(Entity entity, in WeaponMount mount, in ShotAim aim)
            {
                var turnsHull = Units.HasComponent(entity) && !mount.Weapon.Aim.Turreted;
                if (!turnsHull || InsideLookup.HasEnabled(entity))
                {
                    return true;
                }

                return Face(entity, aim.Point, mount.Weapon.Aim.AimTolerance);
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
