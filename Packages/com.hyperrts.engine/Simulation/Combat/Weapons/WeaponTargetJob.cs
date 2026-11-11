using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Leaves each <see cref="WeaponMount.Target"/> set only to what the weapon can shoot this frame; a projectile
    /// shooter takes the nearest incoming one.
    /// </summary>
    [BurstCompile]
    [WithNone(typeof(Offline))]
    [WithPresent(typeof(AttackTarget))]
    public partial struct WeaponTargetJob : IJobEntity
    {
        [ReadOnly] public SpatialIndex Index;
        public OwnerRelations Relations;
        public TargetLookup Targets;
        public ScanSchedule Scans;
        [ReadOnly] public ProjectileGrid Projectiles;

        private void Execute(Entity entity, DynamicBuffer<WeaponMount> mounts, in LocalTransform transform,
            in AttackTarget attack, EnabledRefRO<AttackTarget> attacking, in CombatStance stance, in Owner owner)
        {
            var shared = attacking.ValueRO ? attack.Value : Entity.Null;
            var scanning = Scans.IsDue(entity) && stance.Value != Stance.Passive;
            for (var i = 0; i < mounts.Length; i++)
            {
                var mount = mounts[i];
                if (mount.Weapon.ShootsProjectiles)
                {
                    mount.Target = Incoming(transform.Position, mount, owner.Value);
                }
                else if (i == 0)
                {
                    mount.Target = Primary(mount, attack, attacking.ValueRO);
                }
                else
                {
                    mount.Target = Aim(entity, transform.Position, mount, shared, scanning, owner.Value);
                }

                mounts[i] = mount;
            }
        }

        /// <summary>The nearest hostile projectile it engages inside its range, else the nearest headed for that range.</summary>
        private Entity Incoming(float3 position, in WeaponMount mount, byte owner) =>
            mount.Ready ? Projectiles.Nearest(position, mount.Weapon, owner, Relations) : Entity.Null;

        /// <summary>Engagement's reach, since a longer-ranged extra weapon can put the unit in range before the primary reaches.</summary>
        private Entity Primary(in WeaponMount mount, in AttackTarget attack, bool attacking)
        {
            if (!mount.Ready || !attacking || !attack.InRange)
            {
                return Entity.Null;
            }

            var reached = mount.Weapon.Band(attack.Reach).Contains(attack.Gap);
            return reached && Targets.IsAlive(attack.Value) ? attack.Value : Entity.Null;
        }

        private Entity Aim(Entity entity, float3 position, in WeaponMount mount, Entity shared, bool scanning,
            byte owner)
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

            if (KeepsTarget(entity, position, mount, owner))
            {
                return mount.Target;
            }

            return scanning ? Scan(entity, position, mount.Weapon, owner) : Entity.Null;
        }

        /// <summary>Its own target is still hostile, unhidden and in its reach.</summary>
        private bool KeepsTarget(Entity entity, float3 position, in WeaponMount mount, byte owner)
        {
            var target = mount.Target;
            if (target == Entity.Null || !Targets.IsValidTarget(target, owner, Relations))
            {
                return false;
            }

            return InReach(entity, position, mount.Weapon, target);
        }

        private Entity Scan(Entity entity, float3 position, in Weapon weapon, byte owner)
        {
            var finder = new NearestHostile(Relations, Targets, weapon, owner, entity, position);
            finder.Search(Index, Targets.ScanReach(entity, weapon), true);
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
