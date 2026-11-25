using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Simulation.Movement;
using UnityEngine;

namespace HyperRTS.Editor.Authoring.ModelFill
{
    /// <summary>The Wheel_L01... naming of a model's road wheels, and the axles and steering they imply.</summary>
    internal static class WheelParts
    {
        /// <summary>The name of a road wheel, Wheel_L01 or Wheel_R02, with its side captured; sprockets and idlers don't match.</summary>
        public static readonly Regex RoadWheelName = new(@"^Wheel_([LR])\d+$");

        // Wheels of one axle, or the same wheel in a damaged model, sit within this of each other along the hull.
        private const float AxleTolerance = 0.1f;

        /// <summary>The steering axles of a wheeled hull, each as its distance ahead of the hull centre; empty for tracks.</summary>
        public static List<float> SteeringAxles(LocomotionAuthoring locomotion) =>
            locomotion.Chassis.IsWheeled ? Axles(locomotion).Take(locomotion.steeringAxles).ToList() : new List<float>();

        /// <summary>Whether <paramref name="wheel"/> sits on one of the <see cref="SteeringAxles"/>.</summary>
        public static bool IsOnAxle(LocomotionAuthoring locomotion, List<float> axles, Transform wheel)
        {
            var along = Along(locomotion, wheel);
            return axles.Any(axle => Mathf.Abs(axle - along) < AxleTolerance);
        }

        /// <summary>Each axle of the model's Wheel_L01... wheels, as its distance ahead of the hull centre, front first.</summary>
        public static List<float> Axles(LocomotionAuthoring locomotion)
        {
            var positions = locomotion.GetComponentsInChildren<Transform>(true).Where(part => RoadWheelName.Match(part.name).Groups[1].Value == "L")
                .Select(part => Along(locomotion, part)).OrderByDescending(along => along);
            var axles = new List<float>();
            foreach (var along in positions)
            {
                if (axles.Count == 0 || axles[^1] - along >= AxleTolerance)
                {
                    axles.Add(along);
                }
            }

            return axles;
        }

        private static float Along(LocomotionAuthoring locomotion, Transform part) =>
            locomotion.transform.InverseTransformPoint(part.position).z;
    }
}
