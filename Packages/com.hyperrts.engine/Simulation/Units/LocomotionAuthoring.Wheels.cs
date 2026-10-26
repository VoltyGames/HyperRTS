using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    public partial class LocomotionAuthoring
    {
        private static readonly Regex LeftWheel = new(@"^Wheel_L\d+$");

        // Wheels of one axle, or the same wheel in a damaged model, sit within this of each other along the hull.
        private const float AxleTolerance = 0.1f;

        /// <summary>
        /// Measures <see cref="wheelbase"/> and <see cref="rearAxle"/> from the model's Wheel_L01... wheels: the first
        /// <see cref="steeringAxles"/> axles from the front steer, the rest make the rear axle.
        /// </summary>
        [ContextMenu("Fill From Wheels")]
        public void FillFromWheels()
        {
            var axles = Axles();
            if (axles.Count <= steeringAxles)
            {
                throw new InvalidOperationException(
                    $"'{name}' has {axles.Count} axles of Wheel_L wheels; {steeringAxles} steering axles need more.");
            }

            var front = axles.Take(steeringAxles).Average();
            var rear = axles.Skip(steeringAxles).Average();
            wheelbase = front - rear;
            rearAxle = -rear;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Whether <paramref name="wheel"/> sits on one of the steering axles of a wheeled hull.</summary>
        public bool IsOnSteeringAxle(Transform wheel)
        {
            if (!Chassis.IsWheeled)
            {
                return false;
            }

            var along = Along(wheel);
            return Axles().Take(steeringAxles).Any(axle => Mathf.Abs(axle - along) < AxleTolerance);
        }

        /// <summary>Each axle's distance ahead of the hull centre, front first.</summary>
        private List<float> Axles()
        {
            var positions = GetComponentsInChildren<Transform>(true).Where(part => LeftWheel.IsMatch(part.name))
                .Select(Along).OrderByDescending(along => along);
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

        private float Along(Transform part) => transform.InverseTransformPoint(part.position).z;
    }
}
