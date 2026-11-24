using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// One weapon of an entity, at the same index as its <see cref="WeaponPose"/> and its fire cue's variant; see
    /// <see cref="WeaponMounts"/> for the primary. An interceptor comes last. Server only.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct WeaponMount : IBufferElementData
    {
        /// <summary>Live stats; <c>StatSystem</c> rewrites damage, range and cooldown from the bases.</summary>
        public Weapon Weapon;

        public float BaseDamage;
        public float BaseRange;
        public float BaseCooldown;

        public WeaponCycle Cycle;

        /// <summary>What it shoots this frame; <c>Entity.Null</c> while it holds fire.</summary>
        public Entity Target;

        public Ammo Ammo;

        /// <summary>Research type the owner must have finished; 0 needs none.</summary>
        public int RequiredResearchTypeId;

        /// <summary>Set by the upgrades module once the owner has the required research.</summary>
        public bool Unlocked;

        /// <summary>Held silent by whatever carries it, such as a broken building section.</summary>
        public bool Silenced;

        /// <summary>An extra weapon that may pick its own target when it can't hit the shared one.</summary>
        public bool Independent;

        /// <summary>Shots each firing makes: one, or a squad's living members who carry this weapon; 0 can't fire.</summary>
        public byte Carriers;

        public static WeaponMount Create(in Weapon weapon, in Ammo ammo) => new()
        {
            Weapon = weapon,
            Carriers = 1,
            BaseDamage = weapon.Damage.Amount,
            BaseRange = weapon.Range,
            BaseCooldown = weapon.Cooldown,
            Ammo = ammo,
        };

        /// <summary>An extra weapon aims with its own pose, never the hull, so it fires once that pose is on target.</summary>
        public static WeaponMount CreateExtra(Weapon weapon, in Ammo ammo, int requiredResearchTypeId, bool independent)
        {
            weapon.Aim.Turreted = true;
            var mount = Create(weapon, ammo);
            mount.RequiredResearchTypeId = requiredResearchTypeId;
            mount.Independent = independent;
            return mount;
        }

        public bool Enabled => (RequiredResearchTypeId == 0 || Unlocked) && !Silenced;

        /// <summary>A usable weapon that runs dry and needs reloading.</summary>
        public bool IsLimited => Enabled && Ammo.Limited;

        public bool Ready => Enabled && !Ammo.Empty && Carriers > 0;
    }
}
