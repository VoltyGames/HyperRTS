using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

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
        public float GarrisonFactor;
        public float SplashRadius;
        public float SplashEdgeFactor;
        public bool FriendlyFire;
        public Entity ProjectilePrefab;
        public float ProjectileSpeed;

        /// <summary>What interceptors see its projectiles as.</summary>
        public ProjectileKind ProjectileKind;

        public float AcquireRange;

        /// <summary>Auto-acquired targets are dropped beyond this multiple of the acquire range.</summary>
        public float LeashFactor;

        /// <summary>What the weapon can hit; units and orders never aim it at anything else.</summary>
        public WeaponTargets Targets;

        /// <summary>Targets whose armor multiplier for this damage type falls below it are never picked; 0 picks all.</summary>
        public float MinEffect;

        /// <summary>Where it points and where its shots leave from.</summary>
        public WeaponAim Aim;

        /// <summary>Targets closer than this, edge to edge, can't be hit.</summary>
        public float MinRange;

        /// <summary>Shots per salvo, <see cref="SalvoInterval"/> seconds apart, before the cooldown; 1 fires singly.</summary>
        public int SalvoSize;

        public float SalvoInterval;

        /// <summary>Shots fired so far in the running salvo.</summary>
        public int SalvoShot;

        /// <summary>Metres around the aim point a ballistic shot lands at random; homing shots ignore it.</summary>
        public float Spread;

        /// <summary>Muzzles fired in turn from the shooter's <see cref="WeaponMuzzle"/>s; up to 1 uses <see cref="WeaponAim.Muzzle"/>.</summary>
        public byte MuzzleCount;

        public byte FirstMuzzle;

        public byte NextMuzzle;

        /// <summary>Holds fire until its unit is <see cref="Deployable"/> and deployed.</summary>
        public bool NeedsDeploy;

        /// <summary>Aircraft are air targets; everything else (units on any other layer, buildings) is surface.</summary>
        public static bool CanHit(WeaponTargets targets, NavLayer layer) => targets switch
        {
            WeaponTargets.Surface => layer != NavLayer.Air,
            WeaponTargets.Air => layer == NavLayer.Air,
            _ => true,
        };

        /// <summary>How far it looks for targets: <see cref="AcquireRange"/>, else <paramref name="visionRange"/>, never under <see cref="Range"/>.</summary>
        public readonly float ScanRange(float visionRange) =>
            math.max(AcquireRange > 0f ? AcquireRange : visionRange, Range);

        /// <summary>The hit it deals: full damage to the target plus its splash, if any.</summary>
        public readonly DamageEvent Hit(Entity source, byte faction, float3 origin, Entity target, float3 position) => new()
        {
            Target = target,
            Position = position,
            Origin = origin,
            Source = source,
            SourceFaction = faction,
            Amount = Damage,
            Type = DamageType,
            GarrisonFactor = GarrisonFactor,
            Radius = SplashRadius,
            EdgeFactor = SplashEdgeFactor,
            FriendlyFire = FriendlyFire,
            Reach = Targets,
        };

        public bool Reaches(float gap) => gap >= MinRange && gap <= Range;

        public bool CoolingDown => CooldownRemaining > 0f;

        public void Tick(float deltaTime) => CooldownRemaining = math.max(0f, CooldownRemaining - deltaTime);

        /// <summary>Counts a shot: the next shot of a salvo waits <see cref="SalvoInterval"/>, the last the cooldown.</summary>
        public void StartCooldown()
        {
            SalvoShot++;
            if (SalvoShot < SalvoSize)
            {
                CooldownRemaining = SalvoInterval;
                return;
            }

            SalvoShot = 0;
            CooldownRemaining = Cooldown;
        }

        /// <summary>Drops the rest of a salvo whose target is gone; the cooldown counts from its last shot.</summary>
        public void EndSalvo()
        {
            if (SalvoShot == 0)
            {
                return;
            }

            SalvoShot = 0;
            CooldownRemaining = math.max(0f, CooldownRemaining + Cooldown - SalvoInterval);
        }

        /// <summary>
        /// A homing shot keeps its target only without a <see cref="Spread"/>: one with a spread flies to its scattered
        /// point instead, as a rocket ripple saturates an area. Ballistic shells keep it while it stays put.
        /// </summary>
        public bool KeepsLock => Spread <= 0f || Aim.Trajectory == Trajectory.Ballistic;

        /// <summary><paramref name="aim"/> moved to a random point within <see cref="Spread"/>.</summary>
        public float3 Scatter(float3 aim, uint seed)
        {
            if (Spread <= 0f)
            {
                return aim;
            }

            var random = Random.CreateFromIndex(seed);
            var offset = random.NextFloat2Direction() * Spread * math.sqrt(random.NextFloat());
            return aim + new float3(offset.x, 0f, offset.y);
        }
    }
}
