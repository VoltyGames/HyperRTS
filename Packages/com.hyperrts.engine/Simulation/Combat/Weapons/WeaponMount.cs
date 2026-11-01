using HyperRTS.Simulation.Combat.Targeting;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// An extra weapon beside the primary <see cref="Weapon"/>, which stays the unit's engagement profile. Server only:
    /// mounts fire at the shared <c>AttackTarget</c> when they can, or pick their own if independent.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct WeaponMount : IBufferElementData
    {
        /// <summary>Live stats; <c>StatSystem</c> rewrites damage, range and cooldown from the bases.</summary>
        public Weapon Weapon;

        public float BaseDamage;
        public float BaseRange;
        public float BaseCooldown;

        /// <summary>What the mount shoots this frame; <c>Entity.Null</c> while it holds fire.</summary>
        public Entity Target;

        public Ammo Ammo;

        /// <summary>Research type the owner must have finished; 0 needs none.</summary>
        public int RequiredResearchTypeId;

        /// <summary>Set by the upgrades module once the owner has the required research.</summary>
        public bool Unlocked;

        /// <summary>Index into the pose buffer, and the fire cue's variant; the primary is 0, mounts 1 up.</summary>
        public byte Pose;

        /// <summary>May pick its own target when it can't hit the shared one.</summary>
        public bool Independent;

        public static WeaponMount Create(in Weapon weapon, in Ammo ammo, int requiredResearchTypeId, byte pose,
            bool independent) => new()
        {
            Weapon = Turned(weapon),
            BaseDamage = weapon.Damage,
            BaseRange = weapon.Range,
            BaseCooldown = weapon.Cooldown,
            Ammo = ammo,
            RequiredResearchTypeId = requiredResearchTypeId,
            Pose = pose,
            Independent = independent,
        };

        /// <summary>A mount aims with its own pose, never the hull, so it fires once that pose is on target.</summary>
        private static Weapon Turned(Weapon weapon)
        {
            weapon.Aim.Turreted = true;
            return weapon;
        }

        public bool Enabled => RequiredResearchTypeId == 0 || Unlocked;

        /// <summary>A usable mount that runs dry and needs reloading.</summary>
        public bool IsLimited => Enabled && Ammo.Limited;

        public bool Ready => Enabled && !Ammo.Empty;

        /// <summary>Widens <paramref name="reach"/> and lowers <paramref name="floor"/> to every mount that joins the attack on <paramref name="target"/>.</summary>
        public static void ExtendReach(DynamicBuffer<WeaponMount> mounts, in TargetLookup targets, Entity target,
            ref float reach, ref float floor)
        {
            foreach (var mount in mounts)
            {
                if (mount.JoinsAttack(targets, target))
                {
                    reach = math.max(reach, mount.Weapon.Range);
                    floor = math.min(floor, mount.Weapon.MinRange);
                }
            }
        }

        /// <summary>A ready mount that shares the unit's target and can hurt it.</summary>
        private bool JoinsAttack(in TargetLookup targets, Entity target)
        {
            if (Independent || !Ready)
            {
                return false;
            }

            return targets.CanHurt(target, Weapon);
        }
    }
}
