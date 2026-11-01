using HyperRTS.Core;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat.Projectiles
{
    /// <summary>
    /// Lets a building or unit shoot down hostile projectiles in flight. It aims and fires the weapon mount after the
    /// unit's weapons, plays its Fire cue at that index and its Impact cue as the airburst where a projectile dies.
    /// Drones are units, so it ignores them.
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

        [Tooltip("Holds fire until its Deployable unit has deployed.")]
        public bool needsDeploy;

        [Header("Ammo")]
        [Tooltip("Engagements before it must reload; 0 never runs dry.")]
        [Min(0)]
        public int rounds;

        [Tooltip("Seconds per round refilled.")]
        [Min(0.05f)]
        [ShowIf(nameof(rounds))]
        public float reloadTime = 10f;

        /// <summary>Its mount and pose index: after the primary and extra weapons, or 0 without a weapon.</summary>
        public int Mount => TryGetComponent<WeaponAuthoring>(out var weapon) ? weapon.WeaponCount : 0;

        /// <summary>An interceptor without a weapon, which brings the mount and pose buffers a weapon's baker adds otherwise.</summary>
        public static void Add(EntityWriter writer, in Interceptor interceptor, in WeaponMount mount)
        {
            writer.Add(interceptor);
            writer.AddBuffer<WeaponMount>().Add(mount);
            writer.AddBuffer<WeaponPose>().Resize(1, NativeArrayOptions.ClearMemory);
        }

        public Interceptor Bake() => new()
        {
            Radius = radius,
            KillChance = killChance,
            Kinds = kinds,
            Mount = (byte)Mount,
        };

        /// <summary>The weapon it aims and fires with, its muzzle and pivots relative to <paramref name="root"/>.</summary>
        public WeaponMount BakeMount(IBaker baker, Transform root)
        {
            var weapon = new Weapon
            {
                Cooldown = 1f / engagementsPerSecond,
                SalvoSize = 1,
                NeedsDeploy = needsDeploy,
                Aim = new WeaponAim
                {
                    Turreted = true,
                    TraverseRate = math.radians(aimSpeed),
                    ElevationRate = math.radians(aimSpeed),
                    MinPitch = math.radians(minElevation),
                    MaxPitch = math.radians(maxElevation),
                    AimTolerance = math.radians(aimTolerance),
                },
            };
            weapon.Aim.BakeSockets(baker, root, muzzle, turret, barrel);
            var mount = WeaponMount.Create(weapon, Ammo.Create(rounds, reloadTime, AmmoRefill.Round));
            mount.Intercepts = true;
            return mount;
        }

        public class Baker : Baker<InterceptorAuthoring>
        {
            public override void Bake(InterceptorAuthoring authoring)
            {
                var writer = this.Writer();
                // A weapon's baker adds this mount after its own; the lookup also rebakes when one is added or removed.
                if (GetComponent<WeaponAuthoring>() != null)
                {
                    writer.Add(authoring.Bake());
                    return;
                }

                Add(writer, authoring.Bake(), authoring.BakeMount(this, authoring.transform));
            }
        }
    }
}
