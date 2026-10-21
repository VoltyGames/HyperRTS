using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>
    /// Client-side motion of a unit model's parts: turrets and barrels follow the weapon pose, barrels recoil, wheels
    /// roll, rotors spin up and down and the body tilts. <see cref="FillFromModel"/> finds the parts by their art names.
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

        [Tooltip("The model or hull child that pitches and rolls; none keeps the body level.")]
        public Transform tiltRoot;

        [Tooltip("Length and width of the ground the body sits on, sampled at the four edges.")]
        public Vector2 footprint = new(6f, 3f);

        [Tooltip("Leans with acceleration instead of following the ground.")]
        public bool aircraft;

        /// <summary>Primary plus extra weapons, the <c>WeaponPose</c> indices a slot may follow.</summary>
        public int WeaponCount => TryGetComponent<WeaponAuthoring>(out var weapon) ? 1 + weapon.extraWeapons.Count : 0;

        public static void AddRig<TWriter>(ref TWriter writer, in MotionRig rig, IReadOnlyList<MotionRigPart> parts)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(rig);
            writer.AddBuffer(parts);
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
        }

        /// <summary>A part under this rig, never the rig itself: the unit's own transform is the simulation's.</summary>
        public bool IsBelow(Transform part) => part != transform && part.IsChildOf(transform);

        /// <summary>Fills the slots from parts named Turret, Barrel, Launcher, Wheel_L01, Rotor_Main, Hull and so on.</summary>
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

            tiltRoot = Named("Hull") ?? ModelRoot();
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
                if (tilt == null)
                {
                    return new MotionRig { TiltRoot = Entity.Null, Aircraft = authoring.aircraft };
                }

                DependsOn(tilt);
                return new MotionRig
                {
                    TiltRoot = GetEntity(tilt, TransformUsageFlags.Dynamic),
                    TiltFrame = Quaternion.Inverse(authoring.transform.rotation) * tilt.parent.rotation,
                    TiltRest = tilt.localRotation,
                    Footprint = authoring.footprint,
                    Aircraft = authoring.aircraft,
                };
            }
        }
    }
}
