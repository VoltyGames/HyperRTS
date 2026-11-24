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
        private static readonly Regex LeftWheel = new(@"^Wheel_L\d+$");

        // Wheels of one axle, or the same wheel in a damaged model, sit within this of each other along the hull.
        private const float AxleTolerance = 0.1f;

        /// <summary>Whether <paramref name="wheel"/> sits on one of the steering axles of a wheeled hull.</summary>
        public static bool IsOnSteeringAxle(LocomotionAuthoring locomotion, Transform wheel)
        {
            if (!locomotion.Chassis.IsWheeled)
            {
                return false;
            }

            var along = Along(locomotion, wheel);
            return Axles(locomotion).Take(locomotion.steeringAxles).Any(axle => Mathf.Abs(axle - along) < AxleTolerance);
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
            var positions = locomotion.GetComponentsInChildren<Transform>(true).Where(part => LeftWheel.IsMatch(part.name))
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
