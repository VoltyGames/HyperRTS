using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Client-side motion of a unit or building model's parts: turrets and barrels follow the weapon or interceptor
    /// pose, barrels recoil, wheels roll, rotors spin up and down and a unit's body tilts. <see cref="FillFromModel"/>
    /// finds the parts by their art names.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Animation + "Motion Rig")]
    [Icon(HyperRTSIcons.Animation)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public class MotionRigAuthoring : AuthoringBehaviour
    {
        private static readonly Regex WheelName = new(@"^(Wheel_[LR]\d+|Sprocket_[LR]|Idler_[LR])$");

        [Tooltip("Parts that turn to the weapon's yaw.")]
        public List<TurretSlot> turrets = new();

        [Tooltip("Barrels and launchers that raise to the weapon's pitch.")]
        public List<ElevationSlot> elevations = new();

        [Tooltip("Barrels that kick back on each shot.")]
        public List<RecoilSlot> recoils = new();

        [Tooltip("Road wheels, sprockets and idlers that roll with the ground covered.")]
        public List<WheelSlot> wheels = new();

        [Tooltip("Rotors that spin up in flight and down when docked.")]
        public List<RotorSlot> rotors = new();

        [Tooltip("The model or hull child that pitches and rolls; none keeps the body level, as on buildings.")]
        public Transform tiltRoot;

        [Tooltip("Length and width of the ground the body sits on, sampled at the four edges.")]
        public Vector2 footprint = new(6f, 3f);

        [Tooltip("Ground vehicles: degrees the body squats, dives or rolls per m/s² of acceleration.")]
        public float swayPerAcceleration = 0.6f;

        [Tooltip("Ground vehicles: the most the body sways on its springs, in degrees.")]
        public float maxSway = 3f;

        [Tooltip("Ground vehicles: how fast the springs bounce, in hertz.")]
        public float springFrequency = 1.5f;

        [Tooltip("Ground vehicles: spring damping ratio, where 1 settles without overshoot.")]
        public float springDamping = 0.45f;

        [Tooltip("Ground vehicles: degrees per second a shot rocks the body away from the gun.")]
        public float recoilKick = 12f;

        /// <summary>Primary and extra weapons, then an interceptor: the <c>WeaponPose</c> indices a slot may follow.</summary>
        public int WeaponCount => Weapons() + (TryGetComponent<InterceptorAuthoring>(out _) ? 1 : 0);

        /// <summary>A building stands on levelled ground and never moves, so its body never tilts.</summary>
        private bool IsBuilding => TryGetComponent<BuildingAuthoring>(out _);

        private int Weapons() =>
            TryGetComponent<WeaponAuthoring>(out var weapon) ? 1 + weapon.extraWeapons.Count : 0;

        public static void AddRig<TWriter>(ref TWriter writer, in MotionRig rig, IReadOnlyList<MotionRigPart> parts)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(rig);
            writer.AddBuffer(parts);
            if (parts.Any(part => part.Kind == MotionPartKind.Rotor))
            {
                writer.Add(new RotorSpin());
            }
        }

        /// <summary>Every slot with its part, turrets first so recoil reads the elevated barrel.</summary>
        public IEnumerable<PartSlot> Slots() =>
            turrets.Cast<PartSlot>().Concat(elevations).Concat(recoils).Concat(wheels).Concat(rotors);

        /// <summary>Why the rig can't bake, one line each.</summary>
        public IEnumerable<string> Problems()
        {
            foreach (var slot in Slots())
            {
                var problem = slot.Problem(this);
                if (problem != null)
                {
                    yield return problem;
                }
            }

            if (tiltRoot != null && !IsBelow(tiltRoot))
            {
                yield return $"Tilt root '{tiltRoot.name}' must be a child of '{name}'.";
            }

            if (tiltRoot != null && IsBuilding)
            {
                yield return $"Building '{name}' never tilts; clear its tilt root.";
            }

            var sprung = !TryGetComponent<FlightAuthoring>(out _);
            if (sprung && (springFrequency <= 0f || springDamping <= 0f))
            {
                yield return "'springFrequency' and 'springDamping' must be above zero.";
            }
        }

        public Suspension Suspension() => new()
        {
            Frequency = springFrequency,
            Damping = springDamping,
            PerAcceleration = swayPerAcceleration * Mathf.Deg2Rad,
            MaxSway = maxSway * Mathf.Deg2Rad,
            Kick = recoilKick * Mathf.Deg2Rad,
        };

        /// <summary>A part under this rig, never the rig itself: the unit's own transform is the simulation's.</summary>
        public bool IsBelow(Transform part) => part != transform && part.IsChildOf(transform);

        /// <summary>Fills the slots, and the weapons' turret and barrel pivots, from parts named Turret, Barrel, Launcher, Wheel_L01, Rotor_Main, Hull and so on.</summary>
        public void FillFromModel()
        {
            var parts = GetComponentsInChildren<Transform>(true).Where(IsBelow).ToList();
            Transform Named(string partName) => parts.FirstOrDefault(part => part.name == partName);
            var launcherWeapon = WeaponCount > 1 ? 1 : 0;

            turrets = KeepTuned(turrets, new[] { Named("Turret") }, part => new TurretSlot { part = part });
            elevations = KeepTuned(elevations, new[] { Named("Barrel"), Named("Launcher") }, part => new ElevationSlot
            {
                part = part,
                weapon = part.name == "Launcher" ? launcherWeapon : 0,
            });
            recoils = KeepTuned(recoils, new[] { Named("Barrel") }, part => new RecoilSlot { part = part });
            wheels = KeepTuned(wheels, parts.Where(part => WheelName.IsMatch(part.name)),
                part => new WheelSlot { part = part, radius = WheelSlot.MeshRadius(part, transform) });
            rotors = KeepTuned(rotors, new[] { Named("Rotor_Main"), Named("Rotor_Tail") }, part => new RotorSlot
            {
                part = part,
                axis = part.InverseTransformDirection(part.name == "Rotor_Main" ? transform.up : transform.right),
            });

            FillWeaponPivots();
            FillInterceptorPivots();
            tiltRoot = IsBuilding ? null : Named("Hull") ?? ModelRoot();
            if (tiltRoot != null)
            {
                var size = PartSlot.MeshBounds(tiltRoot, transform).size;
                footprint = new Vector2(size.z, size.x);
            }
        }

        // A model whose only root node is the hull imports with the hull folded into the model object.
        private Transform ModelRoot()
        {
            var part = Slots().Select(slot => slot.part).FirstOrDefault();
            while (part != null && part.parent != transform)
            {
                part = part.parent;
            }

            return part;
        }

        // Shots leave from the muzzle turned about these parts, so each weapon's pivots follow its slots.
        private void FillWeaponPivots()
        {
            if (!TryGetComponent<WeaponAuthoring>(out var weapon))
            {
                return;
            }

            var stats = weapon.extraWeapons.Select(slot => slot.stats).Prepend(weapon.stats).ToList();
            for (var index = 0; index < stats.Count; index++)
            {
                var barrel = elevations.FirstOrDefault(slot => slot.weapon == index)?.part;
                stats[index].barrel = barrel;
                stats[index].turret = Turret(index, barrel);
            }
        }

        private void FillInterceptorPivots()
        {
            if (!TryGetComponent<InterceptorAuthoring>(out var interceptor))
            {
                return;
            }

            interceptor.barrel = elevations.FirstOrDefault(slot => slot.weapon == interceptor.Pose)?.part;
            interceptor.turret = Turret(interceptor.Pose, interceptor.barrel);
        }

        // A weapon turns with its own turret slot, else with the turret its barrel sits on.
        private Transform Turret(int weapon, Transform barrel)
        {
            var own = turrets.FirstOrDefault(slot => slot.weapon == weapon);
            if (own != null)
            {
                return own.part;
            }

            var carrying = barrel != null ? turrets.FirstOrDefault(slot => barrel.IsChildOf(slot.part)) : null;
            return carrying?.part;
        }

        // Keeps the tuned slot of a part already listed, so filling again changes nothing.
        private static List<T> KeepTuned<T>(List<T> slots, IEnumerable<Transform> found, Func<Transform, T> make)
            where T : PartSlot =>
            found.Where(part => part != null)
                .Select(part => slots.FirstOrDefault(slot => slot.part == part) ?? make(part))
                .ToList();

        public class Baker : Baker<MotionRigAuthoring>
        {
            public override void Bake(MotionRigAuthoring authoring)
            {
                var problems = authoring.Problems().ToList();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException($"Motion Rig on '{authoring.name}': {string.Join(" ", problems)}");
                }

                var root = authoring.transform;
                var parts = new List<MotionRigPart>();
                foreach (var slot in authoring.Slots())
                {
                    DependsOn(slot.part);
                    parts.Add(slot.Bake(root, GetEntity(slot.part, TransformUsageFlags.Dynamic)));
                }

                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                AddRig(ref writer, Rig(authoring), parts);
            }

            private MotionRig Rig(MotionRigAuthoring authoring)
            {
                var tilt = authoring.tiltRoot;
                var aircraft = GetComponent<FlightAuthoring>() != null;
                if (tilt == null)
                {
                    return new MotionRig { TiltRoot = Entity.Null, Aircraft = aircraft };
                }

                DependsOn(tilt);
                return new MotionRig
                {
                    TiltRoot = GetEntity(tilt, TransformUsageFlags.Dynamic),
                    TiltFrame = Quaternion.Inverse(authoring.transform.rotation) * tilt.parent.rotation,
                    TiltRest = tilt.localRotation,
                    Footprint = authoring.footprint,
                    Aircraft = aircraft,
                    // Aircraft lean instead, so their zero springs never sway or kick.
                    Suspension = aircraft ? default : authoring.Suspension(),
                };
            }
        }
    }
}
