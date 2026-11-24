using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Simulation.Movement;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>
    /// A wheeled Locomotion's wheelbase and rear axle from its Wheel_L01... wheels: the first steering axles from the
    /// front steer and the rest make the rear axle. Tracks take 0 for both.
    /// </summary>
    internal sealed class LocomotionModelFill : ModelFill<LocomotionAuthoring>
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

        protected override void Fill(LocomotionAuthoring locomotion)
        {
            if (!locomotion.Chassis.IsWheeled)
            {
                locomotion.wheelbase = 0f;
                locomotion.rearAxle = 0f;
                return;
            }

            var axles = Axles(locomotion);
            var steering = locomotion.steeringAxles;
            if (axles.Count <= steering)
            {
                throw new InvalidOperationException(
                    $"'{locomotion.name}' has {axles.Count} axles of Wheel_L wheels; {steering} steering axles need more.");
            }

            var front = axles.Take(steering).Average();
            var rear = axles.Skip(steering).Average();
            locomotion.wheelbase = front - rear;
            locomotion.rearAxle = -rear;
        }

        /// <summary>Each axle of the model's Wheel_L01... wheels, as its distance ahead of the hull centre, front first.</summary>
        private static List<float> Axles(LocomotionAuthoring locomotion)
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
