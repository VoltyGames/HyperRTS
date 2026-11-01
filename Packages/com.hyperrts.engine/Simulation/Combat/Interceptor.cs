using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Shoots down hostile projectiles in flight: the nearest of an engaged kind inside <see cref="Radius"/>, one
    /// engagement per <see cref="Cooldown"/> once its pose is on target. Server only; clients see its pose.
    /// </summary>
    public struct Interceptor : IComponentData
    {
        public float Radius;

        /// <summary>Chance from 0 to 1 that one engagement downs the projectile.</summary>
        public float KillChance;

        public ProjectileKind Kinds;

        public WeaponAim Aim;

        /// <summary>Seconds between engagements.</summary>
        public float Cooldown;

        public float CooldownRemaining;

        /// <summary>Holds fire until its unit is <see cref="Deployable"/> and deployed.</summary>
        public bool NeedsDeploy;

        /// <summary>Index into the pose buffer, after the weapons, and the fire cue's variant.</summary>
        public byte Pose;

        /// <summary>Refills one round per reload time while online.</summary>
        public Ammo Ammo;

        /// <summary>Engagements so far, which seed each kill roll.</summary>
        public uint Engagements;

        public bool Engages(ProjectileKind kind) => (Kinds & kind) != 0;

        public bool Ready => CooldownRemaining <= 0f && !Ammo.Empty;

        public void Tick(float deltaTime)
        {
            CooldownRemaining = math.max(0f, CooldownRemaining - deltaTime);
            Ammo.Reload(deltaTime);
        }

        /// <summary>Spends a round and restarts the cooldown; true when the roll downs <paramref name="target"/>.</summary>
        public bool Engage(Entity self, Entity target)
        {
            CooldownRemaining = Cooldown;
            Ammo.Spend();
            Engagements++;
            // Seeded by shooter, target and count, so the same fight rolls the same.
            var seed = math.hash(new uint4((uint)self.Index, (uint)target.Index, (uint)target.Version, Engagements));
            return Random.CreateFromIndex(seed).NextFloat() < KillChance;
        }
    }
}
