using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Simulation.Animation;
using HyperRTS.Simulation.Combat;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    public partial class MotionRigAuthoring
    {
        private static readonly Regex WheelName = new(@"^(Wheel_[LR]\d+|Sprocket_[LR]|Idler_[LR])$");

        // Propellers turn about the aircraft's nose axis.
        private static readonly Regex PropName = new(@"^Prop_\d\d$");

        // Turret_02 and Launcher_02 aim weapon 1, for a mount or interceptor on a launcher of its own.
        private static readonly Regex NumberedName = new(@"^(Turret|Launcher)_(\d\d)$");

        /// <summary>
        /// Fills the slots, and the weapons' turret and barrel pivots, from parts named Turret, Barrel, Launcher,
        /// Wheel_L01, Rotor_Main, Prop_01, Hull and so on, in every model under it, such as a damaged one.
        /// </summary>
        public void FillFromModel()
        {
            var all = GetComponentsInChildren<Transform>(true).Where(IsBelow).ToList();
            // A part a Machine moves, such as a launcher raised on deploying, isn't posed by the rig too.
            var parts = all.Where(part => !part.TryGetComponent<MachinePartAuthoring>(out _)).ToList();
            IEnumerable<Transform> Named(string partName) => parts.Where(part => part.name == partName);
            var launcherWeapon = WeaponCount > 1 ? 1 : 0;

            var turretParts = parts.Where(part => NumberedName.Match(part.name).Groups[1].Value == "Turret");
            var launcherParts = parts.Where(part => NumberedName.Match(part.name).Groups[1].Value == "Launcher");
            turrets = KeepTuned(turrets, Named("Turret").Concat(turretParts), part => new TurretSlot
            {
                part = part,
                weapon = NumberedWeapon(part, 0),
            });
            elevations = KeepTuned(elevations, Named("Barrel").Concat(Named("Launcher")).Concat(launcherParts),
                part => new ElevationSlot
                {
                    part = part,
                    weapon = NumberedWeapon(part, part.name == "Launcher" ? launcherWeapon : 0),
                });
            recoils = KeepTuned(recoils, Named("Barrel"), part => new RecoilSlot { part = part });
            wheels = KeepTuned(wheels, parts.Where(part => WheelName.IsMatch(part.name)),
                part => new WheelSlot { part = part, radius = WheelSlot.MeshRadius(part, transform) });
            var rotorParts = Named("Rotor_Main").Concat(Named("Rotor_Tail")).Concat(parts.Where(part => PropName.IsMatch(part.name)));
            rotors = KeepTuned(rotors, rotorParts, part => new RotorSlot
            {
                part = part,
                axis = part.InverseTransformDirection(RotorAxis(part.name)),
            });

            var raised = all.Except(parts).FirstOrDefault(part => part.name is "Barrel" or "Launcher");
            FillWeaponPivots(raised);
            FillInterceptorPivots(raised);
            tiltRoot = IsBuilding ? null : Named("Hull").FirstOrDefault() ?? ModelRoot();
            tiltStage = TiltStage();
            if (tiltRoot != null)
            {
                var size = PartSlot.MeshBounds(tiltRoot, transform).size;
                footprint = new Vector2(size.z, size.x);
            }
        }

        // Another damage stage's model tilts at the same path below its visual as the tilt root does below its own.
        private Transform TiltStage()
        {
            if (tiltRoot == null || !TryGetComponent<DamageStagesAuthoring>(out var stages))
            {
                return null;
            }

            var visuals = stages.stages.Where(stage => stage.visual != null).Select(stage => stage.visual.transform).ToList();
            var own = visuals.FirstOrDefault(visual => tiltRoot.IsChildOf(visual));
            var other = visuals.FirstOrDefault(visual => visual != own);
            if (own == null || other == null)
            {
                return null;
            }

            return tiltRoot == own ? other : other.Find(PathBelow(own, tiltRoot));
        }

        private static string PathBelow(Transform root, Transform part) =>
            part.parent == root ? part.name : $"{PathBelow(root, part.parent)}/{part.name}";

        private Vector3 RotorAxis(string partName) => partName switch
        {
            "Rotor_Main" => transform.up,
            "Rotor_Tail" => transform.right,
            _ => transform.forward,
        };

        private static int NumberedWeapon(Transform part, int unnumbered)
        {
            var match = NumberedName.Match(part.name);
            return match.Success ? int.Parse(match.Groups[2].Value) - 1 : unnumbered;
        }

        // A model whose only root node is the hull imports with the hull folded into the model object.
        private Transform ModelRoot()
        {
            var part = GetComponentsInChildren<MeshFilter>(true).Select(mesh => mesh.transform).FirstOrDefault(IsBelow);
            while (part != null && part.parent != transform)
            {
                part = part.parent;
            }

            return part;
        }

        // Shots leave from the muzzle turned about these parts, so each weapon's pivots follow its slots.
        private void FillWeaponPivots(Transform raised)
        {
            if (!TryGetComponent<WeaponAuthoring>(out var weapon))
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

        private void FillInterceptorPivots(Transform raised)
        {
            if (!TryGetComponent<InterceptorAuthoring>(out var interceptor))
            {
                return;
            }

            interceptor.barrel = Barrel(interceptor.Pose, raised);
            interceptor.turret = Turret(interceptor.Pose, interceptor.barrel);
        }

        // Without a barrel slot of its own, a weapon fires from the launcher its Machine raises.
        private Transform Barrel(int weapon, Transform raised) =>
            elevations.FirstOrDefault(slot => slot.weapon == weapon)?.part ?? raised;

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
    }
}
