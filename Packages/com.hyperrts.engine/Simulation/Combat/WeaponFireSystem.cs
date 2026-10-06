using HyperRTS.Core;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Units;
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
    /// <see cref="Projectile"/> when the weapon has a prefab. Unfinished and unpowered buildings stay silent; a shot
    /// reveals a stealthed shooter for its <see cref="Stealth.RevealDuration"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    public partial struct WeaponFireSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<Health> _health;
        private ComponentLookup<UnitTag> _units;
        private ComponentLookup<Stealth> _stealth;
        private DamageWriter _damage;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>();
            _health = state.GetComponentLookup<Health>(true);
            _units = state.GetComponentLookup<UnitTag>(true);
            _stealth = state.GetComponentLookup<Stealth>();
            _damage = new DamageWriter(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transforms.Update(ref state);
            _health.Update(ref state);
            _units.Update(ref state);
            _stealth.Update(ref state);
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());

            // Single-threaded: every shot appends to the one damage queue.
            new FireJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Transforms = _transforms,
                Health = _health,
                Units = _units,
                Stealth = _stealth,
                Damage = _damage,
            }.Schedule();
        }

        [BurstCompile]
        [WithNone(typeof(ConstructionProgress), typeof(Unpowered))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct FireJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<Health> Health;
            [ReadOnly] public ComponentLookup<UnitTag> Units;
            public ComponentLookup<Stealth> Stealth;
            public DamageWriter Damage;

            private void Execute(Entity entity, ref Weapon weapon, in AttackTarget attack,
                EnabledRefRO<AttackTarget> attacking, in Faction faction)
            {
                weapon.CooldownRemaining = math.max(0f, weapon.CooldownRemaining - DeltaTime);

                var target = attack.Value;
                if (!attacking.ValueRO || !attack.InRange ||
                    !Health.TryGetComponent(target, out var health) || health.Current <= 0f)
                {
                    return;
                }

                var targetPosition = Transforms[target].Position;
                if (Units.HasComponent(entity))
                {
                    Face(entity, targetPosition);
                }

                if (weapon.CooldownRemaining > 0f)
                {
                    return;
                }

                weapon.CooldownRemaining = weapon.Cooldown;
                Reveal(entity);
                if (weapon.ProjectilePrefab == Entity.Null)
                {
                    var origin = Transforms[entity].Position;
                    Damage.Add(CombatMath.Hit(weapon, entity, faction.Value, origin, target, targetPosition));
                }
                else
                {
                    Launch(entity, weapon, target, targetPosition, faction);
                }
            }

            private void Reveal(Entity shooter)
            {
                var stealth = Stealth.GetRefRWOptional(shooter);
                if (stealth.IsValid)
                {
                    stealth.ValueRW.RevealTimer = stealth.ValueRO.RevealDuration;
                }
            }

            private void Face(Entity entity, float3 targetPosition)
            {
                var transform = Transforms[entity];
                var direction = targetPosition - transform.Position;
                direction.y = 0f;
                if (math.lengthsq(direction) > 1e-6f)
                {
                    transform.Rotation = quaternion.LookRotationSafe(direction, math.up());
                    Transforms[entity] = transform;
                }
            }

            private void Launch(Entity shooter, in Weapon weapon, Entity target, float3 targetPosition,
                in Faction faction)
            {
                var lift = new float3(0f, ProjectileSystem.FlightHeight, 0f);
                var origin = Transforms[shooter].Position + lift;
                var aim = targetPosition + lift;

                // Keep the prefab's scale; only place and orient the instance.
                var transform = Transforms.HasComponent(weapon.ProjectilePrefab)
                    ? Transforms[weapon.ProjectilePrefab]
                    : LocalTransform.Identity;
                transform.Position = origin;
                transform.Rotation = quaternion.LookRotationSafe(aim - origin, math.up());

                var projectile = Ecb.Instantiate(weapon.ProjectilePrefab);
                Ecb.AddComponent(projectile, transform);
                Ecb.AddComponent(projectile, new Faction { Value = faction.Value });
                Ecb.AddComponent(projectile, new Projectile
                {
                    Speed = weapon.ProjectileSpeed,
                    Hit = CombatMath.Hit(weapon, shooter, faction.Value, origin, target, aim),
                });
            }
        }
    }
}
