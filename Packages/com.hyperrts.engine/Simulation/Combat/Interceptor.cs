using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Shoots down hostile projectiles in flight: the nearest of an engaged kind inside <see cref="Radius"/>, one
    /// engagement per <see cref="Interval"/> once its pose is on target. Server only; clients see its pose.
    /// </summary>
    public struct Interceptor : IComponentData
    {
        public float Radius;

        /// <summary>Seconds between engagements.</summary>
        public float Interval;

        public float CooldownRemaining;

        /// <summary>Chance from 0 to 1 that one engagement downs the projectile.</summary>
        public float KillChance;

        public ProjectileKind Kinds;

        /// <summary>Muzzle, pivots, turn rates, elevation limits and tolerance; its damage and range go unused.</summary>
        public Weapon Aim;

        /// <summary>Index into the pose buffer, after the weapons, and the fire cue's variant.</summary>
        public byte Pose;

        /// <summary>The projectile it tracks this frame; <c>Entity.Null</c> while idle.</summary>
        public Entity Target;

        public int Rounds;

        /// <summary>0 never runs dry.</summary>
        public int MaxRounds;

        /// <summary>Seconds per round refilled while online.</summary>
        public float ReloadTime;

        public float ReloadElapsed;

        /// <summary>Engagements so far, which seed each kill roll.</summary>
        public uint Engagements;

        public bool Engages(ProjectileKind kind) => (Kinds & kind) != 0;

        public bool Ready => CooldownRemaining <= 0f && (MaxRounds == 0 || Rounds > 0);

        public void Tick(float deltaTime)
        {
            CooldownRemaining = math.max(0f, CooldownRemaining - deltaTime);
            Ammo.Reload(ref Rounds, MaxRounds, ref ReloadElapsed, ReloadTime, deltaTime);
        }

        /// <summary>Spends a round and restarts the interval; true when the roll downs <paramref name="target"/>.</summary>
        public bool Engage(Entity self, Entity target)
        {
            CooldownRemaining = Interval;
            Rounds -= MaxRounds > 0 ? 1 : 0;
            Engagements++;
            // Seeded by shooter, target and count, so the same fight rolls the same.
            var seed = math.hash(new uint4((uint)self.Index, (uint)target.Index, (uint)target.Version, Engagements));
            return Random.CreateFromIndex(seed).NextFloat() < KillChance;
        }
    }
}
