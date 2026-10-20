using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
        private ComponentLookup<Health> _health;
        private ComponentLookup<UnitTag> _units;
        private ShotWriter _shots;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _health = state.GetComponentLookup<Health>(true);
            _units = state.GetComponentLookup<UnitTag>(true);
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
            public ShotWriter Shots;

            private void Execute(Entity entity, ref Weapon weapon, in AttackTarget attack,
                EnabledRefRO<AttackTarget> attacking, in Faction faction)
            {
                weapon.CooldownRemaining = math.max(0f, weapon.CooldownRemaining - DeltaTime);

                var target = attack.Value;
                if (!InReach(weapon, attack, attacking.ValueRO) || !Health.IsAlive(HealthLookup, target))
                {
                    return;
                }

                if (Units.HasComponent(entity))
                {
                    Shots.Face(entity, Shots.Position(target));
                }

                if (weapon.CooldownRemaining > 0f || !Shots.TryUseRound(entity))
                {
                    return;
                }

                weapon.CooldownRemaining = weapon.Cooldown;
                Shots.Fire(entity, weapon, CueSlot.Fire, target, faction.Value);
            }

            /// <summary>A longer-ranged mount can put the unit in range before the primary reaches.</summary>
            private static bool InReach(in Weapon weapon, in AttackTarget attack, bool attacking)
            {
                var engaged = attacking && attack.InRange;
                return engaged && attack.Gap <= weapon.Range;
            }
        }
    }
}
