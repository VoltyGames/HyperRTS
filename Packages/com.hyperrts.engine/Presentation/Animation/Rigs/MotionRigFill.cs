using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Presentation.Animation.Machines;
using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Movement;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>Fills a Motion Rig's slots and the weapons' pivots from the parts the art names.</summary>
    internal sealed class MotionRigFill
    {
        private readonly MotionRigAuthoring _rig;

        public MotionRigFill(MotionRigAuthoring rig)
        {
            _rig = rig;
        }

        private static readonly Regex WheelName = new(@"^(Wheel_[LR]\d+|Sprocket_[LR]|Idler_[LR])$");

        // Only road wheels meet the ground; sprockets and idlers hold the track's ends.
        private static readonly Regex RoadWheelName = new(@"^Wheel_([LR])\d+$");

        private static readonly Regex TrackName = new(@"^Track_([LR])$");

        // Propellers turn about the aircraft's nose axis.
        private static readonly Regex PropName = new(@"^Prop_\d\d$");

        // Turret_02 and Launcher_02 aim weapon 1, for an extra weapon or interceptor on a launcher of its own.
        private static readonly Regex NumberedName = new(@"^(Turret|Launcher)_(\d\d)$");

        public void Run()
        {
            var all = _rig.GetComponentsInChildren<Transform>(true).Where(_rig.IsBelow).ToList();
            // A part a Machine moves, such as a launcher raised on deploying, isn't posed by the rig too.
            var parts = all.Where(part => !part.TryGetComponent<MachinePartAuthoring>(out _)).ToList();
            FillAimers(parts);
            _rig.wheels = KeepTuned(_rig.wheels, parts.Where(part => WheelName.IsMatch(part.name)),
                part => new WheelSlot { part = part, radius = WheelSlot.MeshRadius(part, _rig.transform) });
            FillSteering();
            FillTracks(parts);
            FillRotorsAndTrailers(parts);
            FillBody(all, parts);
        }

        private static IEnumerable<Transform> Named(List<Transform> parts, string partName) =>
            parts.Where(part => part.name == partName);

        private void FillAimers(List<Transform> parts)
        {
            var launcherWeapon = _rig.WeaponCount > 1 ? 1 : 0;
            var turretParts = parts.Where(part => NumberedName.Match(part.name).Groups[1].Value == "Turret");
            var launcherParts = parts.Where(part => NumberedName.Match(part.name).Groups[1].Value == "Launcher");
            _rig.turrets = KeepTuned(_rig.turrets, Named(parts, "Turret").Concat(turretParts), part => new TurretSlot
            {
                part = part,
                weapon = NumberedWeapon(part, 0),
            });
            _rig.elevations = KeepTuned(_rig.elevations,
                Named(parts, "Barrel").Concat(Named(parts, "Launcher")).Concat(launcherParts),
                part => new ElevationSlot
                {
                    part = part,
                    weapon = NumberedWeapon(part, part.name == "Launcher" ? launcherWeapon : 0),
                });
            _rig.recoils = KeepTuned(_rig.recoils, Named(parts, "Barrel"), part => new RecoilSlot { part = part });
        }

        private void FillRotorsAndTrailers(List<Transform> parts)
        {
            var rotorParts = Named(parts, "Rotor_Main").Concat(Named(parts, "Rotor_Tail"))
                .Concat(parts.Where(part => PropName.IsMatch(part.name)));
            _rig.rotors = KeepTuned(_rig.rotors, rotorParts, part => new RotorSlot
            {
                part = part,
                axis = part.InverseTransformDirection(RotorAxis(part.name)),
            });
            _rig.trailers = KeepTuned(_rig.trailers, Named(parts, "Trailer"), part => new TrailerSlot
            {
                part = part,
                axleDistance = MeasureAxleDistance(part),
            });
        }

        private void FillBody(List<Transform> all, List<Transform> parts)
        {
            var raised = all.Except(parts).FirstOrDefault(part => part.name is "Barrel" or "Launcher");
            FillWeaponPivots(raised);
            _rig.tiltRoot = _rig.IsBuilding ? null : Named(parts, "Hull").FirstOrDefault() ?? ModelRoot();
            _rig.tiltFollower = TiltFollower();
            if (_rig.tiltRoot != null)
            {
                var size = PartSlot.MeshBounds(_rig.tiltRoot, _rig.transform).size;
                _rig.footprint = new Vector2(size.z, size.x);
            }
        }

        // The wheels on the Locomotion's steering axles steer; refilling after a chassis change updates them.
        private void FillSteering()
        {
            _rig.TryGetComponent<LocomotionAuthoring>(out var locomotion);
            foreach (var wheel in _rig.wheels)
            {
                wheel.steers = locomotion != null && locomotion.IsOnSteeringAxle(wheel.part);
            }
        }

        // Road wheels follow the ground, and each track pairs with its side's road wheels in the same model.
        private void FillTracks(List<Transform> parts)
        {
            foreach (var wheel in _rig.wheels)
            {
                wheel.followsGround = RoadWheelName.IsMatch(wheel.part.name);
            }

            _rig.tracks = parts.Where(part => TrackName.IsMatch(part.name)).Select(part => new TrackSlot
            {
                part = part,
                wheels = RoadWheels(part).ToList(),
            }).ToList();
            foreach (var track in _rig.tracks.Where(track => !track.part.TryGetComponent<TrackMeshAuthoring>(out _)))
            {
                track.part.gameObject.AddComponent<TrackMeshAuthoring>();
            }
        }

        private IEnumerable<Transform> RoadWheels(Transform track)
        {
            var side = TrackName.Match(track.name).Groups[1].Value;
            var model = FindModel(track);
            return _rig.wheels.Select(slot => slot.part)
                .Where(wheel => RoadWheelName.Match(wheel.name).Groups[1].Value == side && FindModel(wheel) == model);
        }

        // The model under the rig that holds a part, such as the intact or the damaged one.
        private Transform FindModel(Transform part)
        {
            while (part.parent != _rig.transform)
            {
                part = part.parent;
            }

            return part;
        }

        // Another damage stage's model tilts at the same path below its visual as the tilt root does below its own.
        private Transform TiltFollower()
        {
            if (_rig.tiltRoot == null || !_rig.TryGetComponent<DamageStagesAuthoring>(out var stages))
            {
                return null;
            }

            var visuals = stages.stages.Where(stage => stage.visual != null).Select(stage => stage.visual.transform).ToList();
            var own = visuals.FirstOrDefault(visual => _rig.tiltRoot.IsChildOf(visual));
            var other = visuals.FirstOrDefault(visual => visual != own);
            if (own == null || other == null)
            {
                return null;
            }

            return _rig.tiltRoot == own ? other : other.Find(PathBelow(own, _rig.tiltRoot));
        }

        private static string PathBelow(Transform root, Transform part) =>
            part.parent == root ? part.name : $"{PathBelow(root, part.parent)}/{part.name}";

        private Vector3 RotorAxis(string partName) => partName switch
        {
            "Rotor_Main" => _rig.transform.up,
            "Rotor_Tail" => _rig.transform.right,
            _ => _rig.transform.forward,
        };

        private static int NumberedWeapon(Transform part, int unnumbered)
        {
            var match = NumberedName.Match(part.name);
            return match.Success ? int.Parse(match.Groups[2].Value) - 1 : unnumbered;
        }

        // A model whose only root node is the hull imports with the hull folded into the model object.
        private Transform ModelRoot()
        {
            var part = _rig.GetComponentsInChildren<MeshFilter>(true).Select(mesh => mesh.transform).FirstOrDefault(_rig.IsBelow);
            while (part != null && part.parent != _rig.transform)
            {
                part = part.parent;
            }

            return part;
        }

        // Shots leave from the muzzle turned about these parts, so each weapon's pivots follow its slots.
        private void FillWeaponPivots(Transform raised)
        {
            if (!_rig.TryGetComponent<WeaponAuthoring>(out var weapon))
            {
                return;
            }

            var stats = weapon.extraWeapons.Select(slot => slot.stats).Prepend(weapon.stats).ToList();
            for (var index = 0; index < stats.Count; index++)
            {
                var barrel = Barrel(index, raised);
                stats[index].barrel = barrel;
                stats[index].turret = Turret(index, barrel);
            }
        }

        // Without a barrel slot of its own, a weapon fires from the launcher its Machine raises.
        private Transform Barrel(int weapon, Transform raised) =>
            _rig.elevations.FirstOrDefault(slot => slot.weapon == weapon)?.part ?? raised;

        // A weapon turns with its own turret slot, else with the turret its barrel sits on.
        private Transform Turret(int weapon, Transform barrel)
        {
            var own = _rig.turrets.FirstOrDefault(slot => slot.weapon == weapon);
            if (own != null)
            {
                return own.part;
            }

            var carrying = barrel != null ? _rig.turrets.FirstOrDefault(slot => barrel.IsChildOf(slot.part)) : null;
            return carrying?.part;
        }

        // Metres from the trailer's pivot back to the middle of the road wheels below it, in the rig's frame.
        private float MeasureAxleDistance(Transform trailer)
        {
            var roadWheels = trailer.GetComponentsInChildren<Transform>(true).Where(child => RoadWheelName.IsMatch(child.name)).ToList();
            if (roadWheels.Count == 0)
            {
                throw new InvalidOperationException($"'{trailer.name}' has no road wheels to measure its axles from.");
            }

            var hitch = _rig.transform.InverseTransformPoint(trailer.position).z;
            return hitch - roadWheels.Average(wheel => _rig.transform.InverseTransformPoint(wheel.position).z);
        }

        private static List<T> KeepTuned<T>(List<T> slots, IEnumerable<Transform> found, Func<Transform, T> make)
            where T : PartSlot =>
            found.Where(part => part != null)
                .Select(part => slots.FirstOrDefault(slot => slot.part == part) ?? make(part))
                .ToList();
    }
}
