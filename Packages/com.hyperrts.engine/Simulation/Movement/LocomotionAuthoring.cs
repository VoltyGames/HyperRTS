using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation.Drive;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// Gives a ground vehicle handling and its <see cref="Navigation.Chassis"/>: acceleration, braking, steering,
    /// turning and reversing. Without it a unit steps straight at full speed, as infantry do.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Entity + "Units/Locomotion")]
    [Icon(HyperRTSIcons.LocomotionAuthoring)]
    [HelpURL(HyperRTSDocs.Movement)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class LocomotionAuthoring : AuthoringBehaviour
    {
        [Tooltip("Speed gained per second.")]
        [Unit("m/s²")]
        [Min(0.1f)]
        public float acceleration = 2.5f;

        [Tooltip("Speed shed per second when braking.")]
        [Unit("m/s²")]
        [Min(0.1f)]
        public float braking = 6f;

        [Tooltip("Fastest the hull yaws: tracks turn at it, wheels hold it as their grip at speed.")]
        [Unit("°/s")]
        [Min(1f)]
        public float turnRate = 50f;

        [Tooltip("How fast the steered wheels turn. Wheels only.")]
        [Unit("°/s")]
        [Min(1f)]
        public float steerRate = 45f;

        [Tooltip("Tightest circle the rear axle drives. 0 turns at any speed. Ignored when it pivots.")]
        [Unit("m")]
        [Min(0f)]
        public float turnRadius;

        [Tooltip("Stops and turns in place when far off its heading, as tracked vehicles do.")]
        public bool pivots = true;

        [Tooltip("Metres from the steering axles' midpoint to the rear axle. 0 for tracks.")]
        [Min(0f)]
        public float wheelbase;

        [Tooltip("Metres the rear axle (the fixed axles' midpoint) sits behind the hull centre. 0 for tracks.")]
        public float rearAxle;

        [Tooltip("How many axles, from the front, steer; Fill From Wheels measures the rest as the rear axle.")]
        [Range(1, 2)]
        public int steeringAxles = 1;

        [Tooltip("Top speed in reverse.")]
        [Unit("m/s")]
        [Min(0.1f)]
        public float reverseSpeed = 2.5f;

        [Tooltip("Longest leg it backs along when turning round would clearly take longer. 0 never reverses.")]
        [Unit("m")]
        [Min(0f)]
        public float reverseRun = 10f;

        public Locomotion Locomotion => new()
        {
            Acceleration = acceleration,
            Braking = braking,
            TurnRate = math.radians(turnRate),
            SteerRate = math.radians(steerRate),
            ReverseSpeed = reverseSpeed,
        };

        public Chassis Chassis => new()
        {
            Wheelbase = wheelbase,
            RearAxle = rearAxle,
            TurnRadius = turnRadius,
            Pivots = pivots,
            ReverseRun = reverseRun,
        };

        public static void Add(EntityWriter writer, Locomotion locomotion)
        {
            writer.Add(locomotion);
            writer.Add<LocomotionState>();
        }

        public class Baker : Baker<LocomotionAuthoring>
        {
            public override void Bake(LocomotionAuthoring authoring)
            {
                var writer = this.Writer();
                Add(writer, authoring.Locomotion);
            }
        }

        private static readonly Regex LeftWheel = new(@"^Wheel_L\d+$");

        // Wheels of one axle, or the same wheel in a damaged model, sit within this of each other along the hull.
        private const float AxleTolerance = 0.1f;

        /// <summary>
        /// Measures <see cref="wheelbase"/> and <see cref="rearAxle"/> from the model's Wheel_L01... wheels: the first
        /// <see cref="steeringAxles"/> axles from the front steer, the rest make the rear axle.
        /// </summary>
        [InspectorButton("Fill from wheels", Glyph = "wand-sparkles")]
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
