using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Leaves each <see cref="WeaponMount.Target"/> set only to what the weapon can shoot this frame: the primary the
    /// <see cref="AttackTarget"/> once it is in reach, an extra weapon the same target when it can hit it, else, if
    /// independent, its own. Interceptors aim themselves.
    /// </summary>
    [BurstCompile]
    [WithNone(typeof(Offline))]
    [WithPresent(typeof(AttackTarget))]
    public partial struct WeaponTargetJob : IJobEntity
    {
        [ReadOnly] public SpatialIndex Index;
        public FactionRelations Relations;
        public TargetLookup Targets;
        public ScanSchedule Scans;

        private void Execute(Entity entity, DynamicBuffer<WeaponMount> mounts, in LocalTransform transform,
            in AttackTarget attack, EnabledRefRO<AttackTarget> attacking, in CombatStance stance, in Faction faction)
        {
            var shared = attacking.ValueRO ? attack.Value : Entity.Null;
            var scanning = Scans.IsDue(entity) && stance.Value != Stance.Passive;
            for (var i = 0; i < mounts.Length; i++)
            {
                var mount = mounts[i];
                if (mount.Intercepts)
                {
                    continue;
                }

                mount.Target = i == 0
                    ? Primary(mount, attack, attacking.ValueRO)
                    : Aim(entity, transform.Position, mount, shared, scanning, faction.Value);
                mounts[i] = mount;
            }
        }

        /// <summary>Engagement's reach, since a longer-ranged extra weapon can put the unit in range before the primary reaches.</summary>
        private Entity Primary(in WeaponMount mount, in AttackTarget attack, bool attacking)
        {
            if (!mount.Ready || !attacking || !attack.InRange)
            {
                return Entity.Null;
            }

            var reached = attack.Gap >= mount.Weapon.MinRange && attack.Gap <= attack.Reach;
            return reached && Targets.IsAlive(attack.Value) ? attack.Value : Entity.Null;
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

            if (KeepsTarget(entity, position, mount, faction))
            {
                return mount.Target;
            }

            return scanning ? Scan(entity, position, mount.Weapon, faction) : Entity.Null;
        }

        /// <summary>Its own target is still hostile, unhidden and in its reach.</summary>
        private bool KeepsTarget(Entity entity, float3 position, in WeaponMount mount, byte faction)
        {
            var target = mount.Target;
            if (target == Entity.Null || !Targets.IsValidTarget(target, faction, Relations))
            {
                return false;
            }

            return InReach(entity, position, mount.Weapon, target);
        }

        private Entity Scan(Entity entity, float3 position, in Weapon weapon, byte faction)
        {
            var radius = Targets.Radius(entity);
            var finder = new NearestHostile(Relations, Targets, weapon, faction, entity, position, radius, true);
            var reach = Targets.ScanReach(entity, weapon) + radius;
            Index.Query(position, reach, ref finder);
            var found = finder.Best != Entity.Null;
            return found && CanShoot(entity, position, weapon, finder.Best) ? finder.Best : Entity.Null;
        }

        private bool CanShoot(Entity entity, float3 position, in Weapon weapon, Entity target) =>
            Targets.IsAlive(target) && InReach(entity, position, weapon, target);

        private bool InReach(Entity entity, float3 position, in Weapon weapon, Entity target)
        {
            if (!Targets.CanHurt(target, weapon) || !Targets.Reaches(entity, position, weapon, target))
            {
                return false;
            }

            return Targets.CanFireAt(entity, position, weapon, target);
        }
    }
}
