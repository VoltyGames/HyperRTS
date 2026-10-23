using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.GameEntities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Lets a building or unit shoot down hostile projectiles in flight. It plays its Fire cue at the pose index after
    /// the weapons and its Impact cue as the airburst where a projectile dies. Drones are units, so it ignores them.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Interceptor")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(GameEntityAuthoring), "a Unit or Building")]
    public class InterceptorAuthoring : AuthoringBehaviour
    {
        [Header("Engagement")]
        [Tooltip("Distance in metres from the pivot at which projectiles are engaged.")]
        [Min(1f)]
        public float radius = 40f;

        [Tooltip("Engagements per second, each a burst or a launch at one projectile.")]
        [Min(0.05f)]
        public float engagementsPerSecond = 2f;

        [Tooltip("Chance that one engagement destroys the projectile; a miss lets it fly on.")]
        [Range(0f, 1f)]
        public float killChance = 0.5f;

        [Tooltip("Projectile kinds it engages.")]
        public ProjectileKind kinds = ProjectileKind.Shell | ProjectileKind.Rocket | ProjectileKind.Missile;

        [Header("Aim")]
        [Tooltip("Child transform the tracer burst leaves from; none aims from the pivot.")]
        public Transform muzzle;

        [Tooltip("Part the muzzle traverses about, such as the turret; none traverses about the pivot.")]
        public Transform turret;

        [Tooltip("Part the muzzle elevates about, such as the barrel or launcher.")]
        public Transform barrel;

        [Tooltip("Degrees per second it traverses and elevates.")]
        [Min(1f)]
        public float aimSpeed = 120f;

        [Tooltip("Lowest elevation in degrees.")]
        [Range(-90f, 0f)]
        public float minElevation;

        [Tooltip("Highest elevation in degrees; set both limits equal for a launcher raised to a fixed angle.")]
        [Range(0f, 90f)]
        public float maxElevation = 85f;

        [Tooltip("Degrees off target, per axis, it may engage at.")]
        [Min(0.1f)]
        public float aimTolerance = 3f;

        [Header("Ammo")]
        [Tooltip("Engagements before it must reload; 0 never runs dry.")]
        [Min(0)]
        public int rounds;

        [Tooltip("Seconds per round refilled while online.")]
        [Min(0.05f)]
        [ShowIf(nameof(rounds))]
        public float reloadTime = 10f;

        /// <summary>Its index in the pose buffer: after the primary and extra weapons, or 0 without a weapon.</summary>
        public int Pose => TryGetComponent<WeaponAuthoring>(out var weapon) ? weapon.WeaponPoses : 0;

        /// <summary>Without a weapon, at pose 0, it brings the pose buffer; a weapon's baker sizes it otherwise.</summary>
        public static void Add<TWriter>(ref TWriter writer, in Interceptor interceptor)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(interceptor);
            if (interceptor.Pose == 0)
            {
                writer.AddBuffer<WeaponPose>().Resize(1, NativeArrayOptions.ClearMemory);
            }
        }

        /// <summary>The baked interceptor, with its muzzle and pivots relative to <paramref name="root"/>.</summary>
        public Interceptor Bake(IBaker baker, Transform root)
        {
            var aim = new Weapon
            {
                Cooldown = 1f / engagementsPerSecond,
                Turreted = true,
                TraverseRate = math.radians(aimSpeed),
                ElevationRate = math.radians(aimSpeed),
                MinPitch = math.radians(minElevation),
                MaxPitch = math.radians(maxElevation),
                AimTolerance = math.radians(aimTolerance),
            };
            if (muzzle != null)
            {
                aim.Muzzle = EffectSetup.Socket(baker, root, muzzle);
                aim.HasMuzzle = true;
            }

            aim.TurretPivot = EffectSetup.Socket(baker, root, turret);
            aim.BarrelPivot = barrel != null ? EffectSetup.Socket(baker, root, barrel) : aim.Muzzle;
            return new Interceptor
            {
                Radius = radius,
                KillChance = killChance,
                Kinds = kinds,
                Aim = aim,
                Pose = (byte)Pose,
                Rounds = rounds,
                MaxRounds = rounds,
                ReloadTime = reloadTime,
            };
        }

        public class Baker : Baker<InterceptorAuthoring>
        {
            public override void Bake(InterceptorAuthoring authoring)
            {
                // Registers the dependency, so adding or removing a weapon rebakes the pose index.
                GetComponent<WeaponAuthoring>();
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                Add(ref writer, authoring.Bake(this, authoring.transform));
            }
        }
    }
}
