using HyperRTS.Simulation.Combat.Targeting;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// One weapon of an entity, at the same index as its <see cref="WeaponPose"/> and its fire cue's variant. An armed
    /// entity's primary is index 0 and sets how it engages; extra weapons follow it, and an interceptor comes last.
    /// Server only: one capacity keeps a single-weapon unit's buffer in its chunk.
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct WeaponMount : IBufferElementData
    {
        /// <summary>Live stats; <c>StatSystem</c> rewrites damage, range and cooldown from the bases.</summary>
        public Weapon Weapon;

        public float BaseDamage;
        public float BaseRange;
        public float BaseCooldown;

        /// <summary>What it shoots this frame; <c>Entity.Null</c> while it holds fire.</summary>
        public Entity Target;

        public Ammo Ammo;

        /// <summary>Research type the owner must have finished; 0 needs none.</summary>
        public int RequiredResearchTypeId;

        /// <summary>Set by the upgrades module once the owner has the required research.</summary>
        public bool Unlocked;

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

        public bool Enabled => RequiredResearchTypeId == 0 || Unlocked;

        /// <summary>A usable weapon that runs dry and needs reloading.</summary>
        public bool IsLimited => Enabled && Ammo.Limited;

        public bool Ready => Enabled && !Ammo.Empty && Carriers > 0;

        /// <summary>The primary, which orders, engagement and the AI read; false when it shoots projectiles.</summary>
        public static bool TryGetPrimary(DynamicBuffer<WeaponMount> mounts, out Weapon weapon)
        {
            var armed = mounts.Length > 0 && !mounts[0].Weapon.ShootsProjectiles;
            weapon = armed ? mounts[0].Weapon : default;
            return armed;
        }

        /// <summary>Reloads the rounds that refill docked, or those that refill anywhere when <paramref name="docked"/> is false.</summary>
        public static void Reload(DynamicBuffer<WeaponMount> mounts, float deltaTime, bool docked)
        {
            for (var i = 0; i < mounts.Length; i++)
            {
                var mount = mounts[i];
                var refillsDocked = mount.Ammo.Refill == AmmoRefill.Docked;
                if (refillsDocked == docked)
                {
                    mount.Ammo.Reload(deltaTime);
                    mounts[i] = mount;
                }
            }
        }

        /// <summary>Every usable weapon with limited rounds is empty, and there is at least one.</summary>
        public static bool IsSpent(DynamicBuffer<WeaponMount> mounts)
        {
            var limited = false;
            foreach (var mount in mounts)
            {
                if (!mount.IsLimited)
                {
                    continue;
                }

                if (!mount.Ammo.Empty)
                {
                    return false;
                }

                limited = true;
            }

            return limited;
        }

        /// <summary>Widens <paramref name="band"/> to every extra weapon that joins the attack on <paramref name="target"/>.</summary>
        public static void ExtendReach(DynamicBuffer<WeaponMount> mounts, in TargetLookup targets, Entity shooter,
            float3 position, Entity target, ref RangeBand band)
        {
            for (var i = 1; i < mounts.Length; i++)
            {
                var mount = mounts[i];
                if (mount.JoinsAttack(targets, target))
                {
                    band.Reach = math.max(band.Reach, targets.Reach(shooter, position, mount.Weapon, target));
                    band.Floor = math.min(band.Floor, mount.Weapon.MinRange);
                }
            }
        }

        /// <summary>A ready weapon that shares the unit's target and can hurt it.</summary>
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
