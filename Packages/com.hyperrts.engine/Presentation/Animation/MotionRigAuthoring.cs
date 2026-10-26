using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
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
    public partial class MotionRigAuthoring : AuthoringBehaviour
    {
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

        [Tooltip("The same body in another model, such as the damaged stage, which tilts and sways with the tilt root.")]
        public Transform tiltFollower;

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
        public float recoilRate = 12f;

        /// <summary>Primary and extra weapons, then an interceptor: the <c>WeaponPose</c> indices a slot may follow.</summary>
        public int WeaponCount
        {
            get
            {
                if (TryGetComponent<InterceptorAuthoring>(out var interceptor))
                {
                    return interceptor.Pose + 1;
                }

                return TryGetComponent<WeaponAuthoring>(out var weapon) ? weapon.WeaponPoses : 0;
            }
        }

        /// <summary>A building stands on levelled ground and never moves, so its body never tilts.</summary>
        private bool IsBuilding => TryGetComponent<BuildingAuthoring>(out _);

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

            if (tiltFollower != null && (tiltRoot == null || !IsBelow(tiltFollower)))
            {
                yield return $"Tilt follower '{tiltFollower.name}' needs a tilt root and must be a child of '{name}'.";
            }

            if (tiltRoot != null && IsBuilding)
            {
                yield return $"Building '{name}' never tilts; clear its tilt root.";
            }

            if (wheels.Any(wheel => wheel.steers) && !HasWheeledLocomotion)
            {
                yield return "Steering wheels need a wheeled Locomotion (a turn radius, no pivots).";
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
            RecoilRate = recoilRate * Mathf.Deg2Rad,
        };

        private bool HasWheeledLocomotion =>
            TryGetComponent<LocomotionAuthoring>(out var locomotion) && locomotion.Chassis.IsWheeled;

        /// <summary>A part under this rig, never the rig itself: the unit's own transform is the simulation's.</summary>
        public bool IsBelow(Transform part) => part != transform && part.IsChildOf(transform);

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
                var locomotion = GetComponent<LocomotionAuthoring>();
                var parts = new List<MotionRigPart>();
                foreach (var slot in authoring.Slots())
                {
                    DependsOn(slot.part);
                    var part = slot.Bake(root, GetEntity(slot.part, TransformUsageFlags.Dynamic));
                    parts.Add(slot is WheelSlot { steers: true } wheel ? wheel.Steering(part, root, locomotion.rearAxle) : part);
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
                    TiltFollower = authoring.tiltFollower != null ? GetEntity(authoring.tiltFollower, TransformUsageFlags.Dynamic) : Entity.Null,
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
