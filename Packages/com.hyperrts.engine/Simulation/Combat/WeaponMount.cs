using Unity.Entities;

namespace HyperRTS.Simulation.Combat
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

        public int Rounds;

        /// <summary>0 never runs dry.</summary>
        public int MaxRounds;

        /// <summary>Seconds per round refilled while docked.</summary>
        public float ReloadTime;

        public float ReloadElapsed;

        /// <summary>Research type the owner must have finished; 0 needs none.</summary>
        public int RequiredResearchTypeId;

        /// <summary>Set by the upgrades module once the owner has the required research.</summary>
        public bool Unlocked;

        /// <summary>Index into the pose buffer, and the fire cue's variant; the primary is 0, mounts 1 up.</summary>
        public byte Pose;

        /// <summary>May pick its own target when it can't hit the shared one.</summary>
        public bool Independent;

        public static WeaponMount Create(in Weapon weapon, int rounds, float reloadTime, int requiredResearchTypeId,
            byte pose, bool independent) => new()
        {
            Weapon = weapon,
            BaseDamage = weapon.Damage,
            BaseRange = weapon.Range,
            BaseCooldown = weapon.Cooldown,
            Rounds = rounds,
            MaxRounds = rounds,
            ReloadTime = reloadTime,
            RequiredResearchTypeId = requiredResearchTypeId,
            Pose = pose,
            Independent = independent,
        };

        public bool Enabled => RequiredResearchTypeId == 0 || Unlocked;

        public bool HasRounds => MaxRounds == 0 || Rounds > 0;

        /// <summary>A usable mount that runs dry and needs reloading.</summary>
        public bool IsLimited => Enabled && MaxRounds > 0;

        public bool Ready => Enabled && HasRounds;

        public void UseRound()
        {
            if (MaxRounds > 0)
            {
                Rounds--;
            }
        }

        /// <summary>Refills one round per <see cref="ReloadTime"/>.</summary>
        public void Reload(float deltaTime) =>
            Ammo.Reload(ref Rounds, MaxRounds, ref ReloadElapsed, ReloadTime, deltaTime);
    }
}
