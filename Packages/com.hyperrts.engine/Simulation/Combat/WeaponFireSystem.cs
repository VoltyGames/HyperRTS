using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Ticks weapon cooldowns and fires at in-range targets: an instant <see cref="DamageEvent"/>, or a launched
    /// <see cref="Projectile"/> when the weapon has a prefab. Unfinished and unpowered buildings stay silent, as do
    /// weapons out of <see cref="Ammo"/>; a shot reveals a stealthed shooter for its <see cref="Stealth.RevealDuration"/>.
    /// A unit without a turreted weapon turns its hull to the target through movement and fires once it faces it. A
    /// turret fires once its pose is on target. Buildings never turn.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(StealthSystem))]
    public partial struct WeaponFireSystem : ISystem
    {
        private ComponentLookup<Health> _health;
        private ComponentLookup<UnitTag> _units;
        private ComponentLookup<Inside> _inside;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<FacingGoal> _facingGoals;
        private ShotWriter _shots;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>(true);
            _units = state.GetComponentLookup<UnitTag>(true);
            _inside = state.GetComponentLookup<Inside>(true);
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _facingGoals = state.GetComponentLookup<FacingGoal>();
            _shots = new ShotWriter(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _health.Update(ref state);
            _units.Update(ref state);
            _inside.Update(ref state);
            _transforms.Update(ref state);
            _facingGoals.Update(ref state);
            _shots.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>(),
                SystemAPI.GetSingletonEntity<CueQueue>(),
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged));

            // Single-threaded: every shot appends to the one damage queue.
            new FireJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                HealthLookup = _health,
                Units = _units,
                InsideLookup = _inside,
                Transforms = _transforms,
                FacingGoals = _facingGoals,
                Shots = _shots,
            }.Schedule();
        }

        [BurstCompile]
        [WithNone(typeof(Offline))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct FireJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<UnitTag> Units;
            [ReadOnly] public ComponentLookup<Inside> InsideLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<FacingGoal> FacingGoals;
            public ShotWriter Shots;

            private void Execute(Entity entity, ref Weapon weapon, in AttackTarget attack,
                EnabledRefRO<AttackTarget> attacking, in Faction faction)
            {
                weapon.Tick(DeltaTime);

                var target = attack.Value;
                if (!InReach(weapon, attack, attacking.ValueRO) || !Health.IsAlive(HealthLookup, target))
                {
                    return;
                }

                // A garrisoned passenger fires out of any side of its building without turning.
                var turnsHull = Units.HasComponent(entity) && !weapon.Turreted && !InsideLookup.HasEnabled(entity);
                if (turnsHull && !Face(entity, Transforms[target].Position, weapon.AimTolerance))
                {
                    return;
                }

                Shots.TryFire(entity, ref weapon, 0, target, faction.Value, true);
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

            /// <summary>A longer-ranged mount can put the unit in range before the primary reaches.</summary>
            private static bool InReach(in Weapon weapon, in AttackTarget attack, bool attacking)
            {
                var engaged = attacking && attack.InRange;
                return engaged && weapon.Reaches(attack.Gap);
            }
        }
    }
}
