using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
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
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(EngagementSystem))]
    [UpdateBefore(typeof(StealthSystem))]
    public partial struct WeaponFireSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<Health> _health;
        private ComponentLookup<UnitTag> _units;
        private ComponentLookup<Stealth> _stealth;
        private ComponentLookup<Ammo> _ammo;
        private DamageWriter _damage;
        private CueWriter _cues;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>();
            _health = state.GetComponentLookup<Health>(true);
            _units = state.GetComponentLookup<UnitTag>(true);
            _stealth = state.GetComponentLookup<Stealth>();
            _ammo = state.GetComponentLookup<Ammo>();
            _damage = new DamageWriter(ref state);
            _cues = new CueWriter(ref state);
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transforms.Update(ref state);
            _health.Update(ref state);
            _units.Update(ref state);
            _stealth.Update(ref state);
            _ammo.Update(ref state);
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
            _cues.Update(ref state, SystemAPI.GetSingletonEntity<CueQueue>());

            // Single-threaded: every shot appends to the one damage queue.
            new FireJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Transforms = _transforms,
                HealthLookup = _health,
                Units = _units,
                Stealth = _stealth,
                Ammo = _ammo,
                Damage = _damage,
                Cues = _cues,
            }.Schedule();
        }

        [BurstCompile]
        [WithNone(typeof(Offline))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct FireJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer Ecb;
            public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<Health> HealthLookup;
            [ReadOnly] public ComponentLookup<UnitTag> Units;
            public ComponentLookup<Stealth> Stealth;
            public ComponentLookup<Ammo> Ammo;
            public DamageWriter Damage;
            public CueWriter Cues;

            private void Execute(Entity entity, ref Weapon weapon, in AttackTarget attack,
                EnabledRefRO<AttackTarget> attacking, in Faction faction)
            {
                weapon.CooldownRemaining = math.max(0f, weapon.CooldownRemaining - DeltaTime);

                var target = attack.Value;
                var engaged = attacking.ValueRO && attack.InRange;
                if (!engaged || !Health.IsAlive(HealthLookup, target))
                {
                    return;
                }

                var targetPosition = Transforms[target].Position;
                if (Units.HasComponent(entity))
                {
                    Face(entity, targetPosition);
                }

                if (weapon.CooldownRemaining > 0f || !TryUseRound(entity))
                {
                    return;
                }

                weapon.CooldownRemaining = weapon.Cooldown;
                Reveal(entity);
                var place = Transforms[entity];
                var origin = place.Position;
                PlayFire(entity, place, targetPosition, faction.Value);
                if (weapon.ProjectilePrefab == Entity.Null)
                {
                    var hit = CombatMath.Hit(weapon, entity, faction.Value, origin, target, targetPosition);
                    Damage.Add(hit);
                    Cues.AddImpact(Cues.TypeIdFor(entity, CueSlot.Impact), hit, place.Forward());
                }
                else
                {
                    Launch(entity, weapon, target, targetPosition, faction);
                }
            }

            private bool TryUseRound(Entity shooter)
            {
                if (!Ammo.TryGetRefRW(shooter, out var ammo))
                {
                    return true;
                }

                if (ammo.ValueRO.Current <= 0)
                {
                    return false;
                }

                ammo.ValueRW.Current--;
                return true;
            }

            /// <summary>The fire cue faces the target level, the way the shooter turned.</summary>
            private void PlayFire(Entity shooter, in LocalTransform transform, float3 targetPosition, byte faction)
            {
                var toward = targetPosition - transform.Position;
                toward.y = 0f;
                Cues.Play(shooter, CueSlot.Fire, transform, math.normalizesafe(toward, transform.Forward()), faction);
            }

            private void Reveal(Entity shooter)
            {
                if (Stealth.TryGetRefRW(shooter, out var stealth))
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
                var lift = new float3(0f, weapon.ProjectileHeight, 0f);
                var origin = Transforms[shooter].Position + lift;
                var aim = targetPosition + lift;

                // Keep the prefab's scale; only place and orient the instance.
                var transform = Transforms.HasComponent(weapon.ProjectilePrefab)
                    ? Transforms[weapon.ProjectilePrefab]
                    : LocalTransform.Identity;
                transform.Position = origin;
                transform.Rotation = quaternion.LookRotationSafe(aim - origin, math.up());

                var projectile = Ecb.Instantiate(weapon.ProjectilePrefab);
                Ecb.Place(projectile, transform);
                Ecb.AddComponent(projectile, new Faction { Value = faction.Value });
                Ecb.AddComponent(projectile, new Projectile
                {
                    Speed = weapon.ProjectileSpeed,
                    Height = weapon.ProjectileHeight,
                    ImpactTypeId = Cues.TypeIdFor(shooter, CueSlot.Impact),
                    Hit = CombatMath.Hit(weapon, shooter, faction.Value, origin, target, aim),
                });
            }
        }
    }
}
