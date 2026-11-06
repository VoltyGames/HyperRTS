using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Core;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Movement;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Rigs
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

        [Tooltip("Tracks whose lower run bends over the lift of their road wheels.")]
        public List<TrackSlot> tracks = new();

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

        /// <summary>Primary and extra weapons, interceptors among them: the <c>WeaponPose</c> indices a slot may follow.</summary>
        public int WeaponCount => TryGetComponent<WeaponAuthoring>(out var weapon) ? weapon.WeaponCount : 0;

        /// <summary>A building stands on levelled ground and never moves, so its body never tilts.</summary>
        private bool IsBuilding => TryGetComponent<BuildingAuthoring>(out _);

        public static void AddRig(EntityWriter writer, in MotionRig rig, IReadOnlyList<MotionRigPart> parts)
        {
            writer.Add(rig);
            writer.AddBuffer(parts);
            if (parts.Any(part => part.Kind == MotionPartKind.Rotor))
            {
                writer.Add(new RotorSpin());
            }
        }

        public static void AddTracks(EntityWriter writer, IReadOnlyList<MotionRigTrack> tracks, IReadOnlyList<TrackWheel> wheels)
        {
            writer.AddBuffer(tracks);
            writer.AddBuffer(wheels);
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

            foreach (var track in tracks)
            {
                var problem = track.Problem(this);
                if (problem != null)
                {
                    yield return problem;
                }
            }

            foreach (var problem in BodyProblems())
            {
                yield return problem;
            }
        }

        private IEnumerable<string> BodyProblems()
        {
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
                var rig = Rig(authoring);
                var parts = new List<MotionRigPart>();
                var slots = authoring.Slots().ToList();
                foreach (var slot in slots)
                {
                    DependsOn(slot.part);
                    var part = slot.Bake(root, GetEntity(slot.part, TransformUsageFlags.Dynamic));
                    // An aircraft's wheels hang from the air, with no ground to meet.
                    part.Travel = rig.Aircraft ? 0f : part.Travel;
                    parts.Add(slot is WheelSlot { steers: true } wheel ? wheel.Steering(part, root, locomotion.rearAxle) : part);
                }

                var writer = this.Writer();
                AddRig(writer, rig, parts);
                if (authoring.tracks.Count > 0)
                {
                    BakeTracks(authoring, slots, writer);
                }
            }

            // Each track mesh shares its track's wheel entries, which run from the front.
            private void BakeTracks(MotionRigAuthoring authoring, List<PartSlot> slots, EntityWriter writer)
            {
                var tracks = new List<MotionRigTrack>();
                var wheels = new List<TrackWheel>();
                foreach (var track in authoring.tracks)
                {
                    DependsOn(track.part);
                    var first = wheels.Count;
                    wheels.AddRange(track.Stations(authoring.transform).Select(placed => new TrackWheel
                    {
                        Part = slots.FindIndex(slot => slot.part == placed.Wheel),
                        Station = placed.Station,
                    }));
                    tracks.AddRange(track.Meshes().Select(mesh => new MotionRigTrack
                    {
                        Mesh = GetEntity(mesh, TransformUsageFlags.Renderable),
                        FirstWheel = first,
                        WheelCount = wheels.Count - first,
                    }));
                }

                AddTracks(writer, tracks, wheels);
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
