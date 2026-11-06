using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>One weapon's attack stats, held by its <see cref="WeaponMount"/>; range is measured edge to edge between footprints.</summary>
    public struct Weapon
    {
        public float Range;
        public DamageProfile Damage;
        public float Cooldown;
        public float CooldownRemaining;
        public Entity ProjectilePrefab;
        public float ProjectileSpeed;

        /// <summary>What interceptors see its projectiles as.</summary>
        public ProjectileKind ProjectileKind;

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

        /// <summary>
        /// Half angle in radians of the cone it fires into, about the heading its unit deployed at or else its facing;
        /// 0 fires all round.
        /// </summary>
        public float FiringArc;

        /// <summary>Aircraft are air targets; everything else (units on any other layer, buildings) is surface.</summary>
        public static bool CanHit(WeaponTargets targets, NavLayer layer) => targets switch
        {
            WeaponTargets.Surface => layer != NavLayer.Air,
            WeaponTargets.Air => layer == NavLayer.Air,
            _ => true,
        };

        /// <summary>The distances it hits from: its minimum range up to <paramref name="reach"/>.</summary>
        public readonly RangeBand Band(float reach) => new(MinRange, reach);

        /// <summary>Whether a target at <paramref name="toTarget"/> (XZ, any length) lies within the firing arc about <paramref name="heading"/>.</summary>
        public readonly bool InFiringArc(float2 heading, float2 toTarget)
        {
            if (FiringArc <= 0f)
            {
                return true;
            }

            var direction = math.normalizesafe(toTarget);
            return direction.Equals(float2.zero) || math.dot(heading, direction) >= math.cos(FiringArc);
        }

        /// <summary>Whether the target is within the firing arc of a shooter standing at <paramref name="place"/>.</summary>
        public readonly bool InFiringArc(in ComponentLookup<Deployable> deployables, Entity shooter, in LocalTransform place,
            float3 targetPosition)
        {
            if (FiringArc <= 0f)
            {
                return true;
            }

            return InFiringArc(Deployable.Heading(deployables, shooter, place), (targetPosition - place.Position).xz);
        }

        public bool Reaches(float gap) => gap >= MinRange && gap <= Range;

        /// <summary>Direct fire and rockets need a clear line to the target; lobbed shells don't.</summary>
        public readonly bool NeedsLineOfFire => Aim.Trajectory != Trajectory.Ballistic;

        /// <summary>Launches rounds, which hit whatever is in the way, rather than hitting at once.</summary>
        public readonly bool FiresRounds => ProjectilePrefab != Entity.Null;

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
