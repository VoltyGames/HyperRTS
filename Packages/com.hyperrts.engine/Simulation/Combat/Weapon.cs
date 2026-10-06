using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Attack stats; range is measured edge to edge between footprints.</summary>
    public struct Weapon : IComponentData
    {
        public float Range;
        public float Damage;
        public float Cooldown;
        public float CooldownRemaining;
        public UnityObjectRef<DamageType> DamageType;
        public float SplashRadius;
        public float SplashEdgeFactor;
        public bool FriendlyFire;
        public Entity ProjectilePrefab;
        public float ProjectileSpeed;

        /// <summary>Height above the shooter's and target's pivots that projectiles fly at; 0 flies pivot to pivot.</summary>
        public float ProjectileHeight;

        public float AcquireRange;

        /// <summary>What the weapon can hit; units and orders never aim it at anything else.</summary>
        public WeaponTargets Targets;
    }
}
