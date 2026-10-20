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
    /// Aims and fires each ready <see cref="WeaponMount"/>: at the shared <see cref="AttackTarget"/> when it can hit
    /// it, else, if independent, at the nearest spotted hostile in its own range. Offline shooters stay silent.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(WeaponFireSystem))]
    [UpdateBefore(typeof(StealthSystem))]
    public partial struct MountFireSystem : ISystem
    {
        private TargetLookup _targets;
        private EntityQuery _fogQuery;
        private BufferLookup<ArmorModifier> _armor;
        private ShotWriter _shots;
        private uint _frame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targets = new TargetLookup(ref state);
            _fogQuery = TargetLookup.FogQuery(ref state);
            _armor = state.GetBufferLookup<ArmorModifier>(true);
            _shots = new ShotWriter(ref state);
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
            state.RequireForUpdate<FogOfWar>();
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targets.Update(ref state, _fogQuery);
            _armor.Update(ref state);
            _frame++;

            new AimJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Relations = SystemAPI.GetSingleton<FactionRelations>(),
                Targets = _targets,
                Armor = _armor,
                Slot = (int)(_frame % TargetAcquisitionSystem.ScanInterval),
            }.ScheduleParallel();

            _shots.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>(),
                SystemAPI.GetSingletonEntity<CueQueue>(),
                SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged));

            // Single-threaded: every shot appends to the one damage queue.
            new ShotJob { DeltaTime = SystemAPI.Time.DeltaTime, Shots = _shots }.Schedule();
        }

        /// <summary>Leaves each mount's <see cref="WeaponMount.Target"/> set only to what it can shoot this frame.</summary>
        [BurstCompile]
        [WithNone(typeof(Offline))]
        [WithPresent(typeof(AttackTarget))]
        private partial struct AimJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public FactionRelations Relations;
            public TargetLookup Targets;
            [ReadOnly] public BufferLookup<ArmorModifier> Armor;
            public int Slot;

            private void Execute(Entity entity, DynamicBuffer<WeaponMount> mounts, in LocalTransform transform,
                in AttackTarget attack, EnabledRefRO<AttackTarget> attacking, in CombatStance stance,
                in Faction faction)
            {
                var shared = attacking.ValueRO ? attack.Value : Entity.Null;
                var onSlot = entity.Index % TargetAcquisitionSystem.ScanInterval == Slot;
                var scanning = onSlot && stance.Value != Stance.Passive;
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    mount.Target = Aim(entity, transform.Position, mount, shared, scanning, faction.Value);
                    mounts[i] = mount;
                }
            }

            private Entity Aim(Entity entity, float3 position, in WeaponMount mount, Entity shared, bool scanning,
                byte faction)
            {
                if (!mount.Ready)
                {
                    return Entity.Null;
                }

                if (shared != Entity.Null && CanShoot(entity, position, mount.Weapon, shared))
                {
                    return shared;
                }

                if (!mount.Independent)
                {
                    return Entity.Null;
                }

                if (KeepsOwn(entity, position, mount, faction))
                {
                    return mount.Target;
                }

                return scanning ? Scan(entity, position, mount.Weapon, faction) : Entity.Null;
            }

            /// <summary>Its own target is still hostile, unhidden and in its reach.</summary>
            private bool KeepsOwn(Entity entity, float3 position, in WeaponMount mount, byte faction)
            {
                var own = mount.Target;
                if (own == Entity.Null || !Targets.IsValidTarget(own, faction, Relations))
                {
                    return false;
                }

                return CanShoot(entity, position, mount.Weapon, own);
            }

            private Entity Scan(Entity entity, float3 position, in Weapon weapon, byte faction)
            {
                var radius = Targets.Radius(entity);
                var finder = new NearestHostile(Relations, Targets, Armor, weapon, faction, position, radius);
                Index.Query(position, weapon.Range + radius, ref finder);
                var found = finder.Best != Entity.Null;
                return found && CanShoot(entity, position, weapon, finder.Best) ? finder.Best : Entity.Null;
            }

            private bool CanShoot(Entity entity, float3 position, in Weapon weapon, Entity target)
            {
                if (!Targets.IsAlive(target) || !Targets.CanHit(target, weapon.Targets))
                {
                    return false;
                }

                if (!weapon.Affects(Armor, target))
                {
                    return false;
                }

                var gap = EntityRadius.EdgeDistance(position, Targets.Radius(entity), Targets.Position(target),
                    Targets.Radius(target));
                return weapon.Reaches(gap);
            }
        }

        [BurstCompile]
        [WithNone(typeof(Offline))]
        private partial struct ShotJob : IJobEntity
        {
            public float DeltaTime;
            public ShotWriter Shots;

            private void Execute(Entity entity, DynamicBuffer<WeaponMount> mounts, in Faction faction)
            {
                for (var i = 0; i < mounts.Length; i++)
                {
                    var mount = mounts[i];
                    mount.Weapon.CooldownRemaining = math.max(0f, mount.Weapon.CooldownRemaining - DeltaTime);
                    if (mount.Target != Entity.Null && mount.Weapon.CooldownRemaining <= 0f)
                    {
                        mount.Weapon.CooldownRemaining = mount.Weapon.Cooldown;
                        mount.UseRound();
                        Shots.Fire(entity, mount.Weapon, mount.Pose, WeaponMount.FireSlot(i), mount.Target,
                            faction.Value);
                    }

                    mounts[i] = mount;
                }
            }
        }
    }
}
