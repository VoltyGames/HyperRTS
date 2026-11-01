using HyperRTS.Simulation.Combat.Weapons;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Projectiles
{
    /// <summary>
    /// Shoots down hostile projectiles in flight: the nearest of an engaged kind inside <see cref="Radius"/>, once the
    /// pose of its <see cref="WeaponMount"/> is on target, at that weapon's cooldown and rounds. Server only; clients
    /// see its pose.
    /// </summary>
    public struct Interceptor : IComponentData
    {
        public float Radius;

        /// <summary>Chance from 0 to 1 that one engagement downs the projectile.</summary>
        public float KillChance;

        public ProjectileKind Kinds;

        /// <summary>Index of the mount it aims and fires, after the weapons.</summary>
        public byte Mount;

        /// <summary>Engagements so far, which seed each kill roll.</summary>
        public uint Engagements;

        public bool Engages(ProjectileKind kind) => (Kinds & kind) != 0;

        /// <summary>Spends a round and restarts the mount's cooldown; true when the roll downs <paramref name="target"/>.</summary>
        public bool Engage(ref WeaponMount mount, Entity self, Entity target)
        {
            mount.Weapon.StartCooldown();
            mount.Ammo.Spend();
            Engagements++;
            // Seeded by shooter, target and count, so the same fight rolls the same.
            var seed = math.hash(new uint4((uint)self.Index, (uint)target.Index, (uint)target.Version, Engagements));
            return Random.CreateFromIndex(seed).NextFloat() < KillChance;
        }
    }
}
