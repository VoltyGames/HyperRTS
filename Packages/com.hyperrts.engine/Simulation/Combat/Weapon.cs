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

        public Trajectory Trajectory;

        /// <summary>Apex of an arcing shot as a fraction of the launch-to-aim ground distance.</summary>
        public float ArcHeight;

        /// <summary>Targets closer than this, edge to edge, can't be hit.</summary>
        public float MinRange;

        /// <summary>A turret aims instead of the hull turning to fire, and fires once it is on target.</summary>
        public bool Turreted;

        /// <summary>Radians per second the pose turns and raises.</summary>
        public float TraverseRate;
        public float ElevationRate;

        /// <summary>Elevation limits in radians, within the pose's -96 to 95 degrees.</summary>
        public float MinPitch;
        public float MaxPitch;

        /// <summary>Radians off target, per axis, a turret may fire at.</summary>
        public float AimTolerance;

        /// <summary>Points in the shooter's space the muzzle traverses and elevates about.</summary>
        public float3 TurretPivot;
        public float3 BarrelPivot;

        public bool Reaches(float gap) => gap >= MinRange && gap <= Range;

        public bool CoolingDown => CooldownRemaining > 0f;

        public void Tick(float deltaTime) => CooldownRemaining = math.max(0f, CooldownRemaining - deltaTime);

        public void StartCooldown() => CooldownRemaining = Cooldown;

        public bool Affects(in BufferLookup<ArmorModifier> armor, Entity target) =>
            MinEffect <= 0f || CombatMath.ArmorMultiplier(armor, target, DamageType) >= MinEffect;

        /// <summary>Where a projectile starts: the posed muzzle, else <see cref="ProjectileHeight"/> above the pivot.</summary>
        public float3 LaunchPoint(in LocalTransform shooter, in WeaponPose pose) =>
            HasMuzzle ? shooter.TransformPoint(PosedMuzzle(pose)) : shooter.Position + new float3(0f, ProjectileHeight, 0f);

        /// <summary>Where an instant hit comes from, for directional armor.</summary>
        public float3 HitOrigin(in LocalTransform shooter, in WeaponPose pose) =>
            HasMuzzle ? shooter.TransformPoint(PosedMuzzle(pose)) : shooter.Position;

        /// <summary>The muzzle raised about the barrel pivot, then turned about the turret pivot.</summary>
        public float3 PosedMuzzle(in WeaponPose pose)
        {
            var raised = BarrelPivot + math.rotate(quaternion.AxisAngle(math.right(), -pose.PitchAngle), Muzzle - BarrelPivot);
            return TurretPivot + math.rotate(quaternion.AxisAngle(math.up(), pose.YawAngle), raised - TurretPivot);
        }

        /// <summary>Yaw and pitch that point the posed weapon at <paramref name="aim"/>; a hull-fixed weapon keeps yaw 0.</summary>
        public float2 AimAngles(in LocalTransform shooter, in WeaponPose pose, float3 aim)
        {
            var from = LaunchPoint(shooter, pose);
            var local = math.rotate(math.inverse(shooter.Rotation), aim - from);
            var yaw = Turreted ? math.atan2(local.x, local.z) : 0f;
            return new float2(yaw, math.clamp(Elevation(from, aim), MinPitch, MaxPitch));
        }

        /// <summary>Turns <paramref name="pose"/> toward <paramref name="angles"/> at the traverse and elevation rates.</summary>
        public void Turn(ref WeaponPose pose, float2 angles, float deltaTime) =>
            pose.Turn(angles, new float2(TraverseRate, ElevationRate) * deltaTime);

        /// <summary>The pose points within <see cref="AimTolerance"/> of <paramref name="angles"/> on both axes.</summary>
        public bool Aligned(in WeaponPose pose, float2 angles) => math.all(pose.Gap(angles) <= AimTolerance);

        /// <summary>Radians above level to fire from <paramref name="from"/> at <paramref name="aim"/>, lobbed if arcing.</summary>
        public float Elevation(float3 from, float3 aim)
        {
            var ground = math.max(math.distance(from.xz, aim.xz), 0.01f);
            var slope = (aim.y - from.y) / ground;
            // An arc of apex ArcHeight times the ground distance leaves at a slope of 4 ArcHeight above the chord.
            return math.atan(Trajectory == Trajectory.Direct ? slope : slope + 4f * ArcHeight);
        }
    }
}
