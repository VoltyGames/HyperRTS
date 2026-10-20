using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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

        /// <summary>Auto-acquired targets are dropped beyond this multiple of the acquire range.</summary>
        public float LeashFactor;

        /// <summary>What the weapon can hit; units and orders never aim it at anything else.</summary>
        public WeaponTargets Targets;

        /// <summary>Targets whose armor multiplier for this damage type falls below it are never picked; 0 picks all.</summary>
        public float MinEffect;

        /// <summary>Shot spawn point in the shooter's local space, used when <see cref="HasMuzzle"/>.</summary>
        public float3 Muzzle;

        public bool HasMuzzle;

        public bool Affects(in BufferLookup<ArmorModifier> armor, Entity target) =>
            MinEffect <= 0f || CombatMath.ArmorMultiplier(armor, target, DamageType) >= MinEffect;

        /// <summary>Where a projectile starts: the muzzle, else <see cref="ProjectileHeight"/> above the pivot.</summary>
        public float3 LaunchPoint(in LocalTransform shooter) =>
            HasMuzzle ? shooter.TransformPoint(Muzzle) : shooter.Position + new float3(0f, ProjectileHeight, 0f);

        /// <summary>Where an instant hit comes from, for directional armor.</summary>
        public float3 HitOrigin(in LocalTransform shooter) =>
            HasMuzzle ? shooter.TransformPoint(Muzzle) : shooter.Position;
    }
}
