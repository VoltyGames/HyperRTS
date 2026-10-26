using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Gives a ground vehicle handling and its <see cref="Navigation.Chassis"/>: acceleration, braking, steering,
    /// turning and reversing. Without it a unit steps straight at full speed, as infantry do.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Locomotion")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Units)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public partial class LocomotionAuthoring : AuthoringBehaviour
    {
        [Tooltip("Speed gained per second, in m/s2.")]
        [Min(0.1f)]
        public float acceleration = 2.5f;

        [Tooltip("Speed shed per second when braking, in m/s2.")]
        [Min(0.1f)]
        public float braking = 6f;

        [Tooltip("Fastest the hull yaws, in degrees per second: tracks turn at it, wheels hold it as their grip at speed.")]
        [Min(1f)]
        public float turnRate = 50f;

        [Tooltip("How fast the steered wheels turn, in degrees per second. Wheels only.")]
        [Min(1f)]
        public float steerRate = 45f;

        [Tooltip("Tightest circle the rear axle drives, in metres. 0 turns at any speed. Ignored when it pivots.")]
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

        [Tooltip("Top speed in reverse, in m/s.")]
        [Min(0.1f)]
        public float reverseSpeed = 2.5f;

        [Tooltip("Longest leg it backs along, in metres, when turning round would clearly take longer. 0 never reverses.")]
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

        public static void Add<TWriter>(ref TWriter writer, Locomotion locomotion)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(locomotion);
            writer.Add<LocomotionState>();
        }

        public class Baker : Baker<LocomotionAuthoring>
        {
            public override void Bake(LocomotionAuthoring authoring)
            {
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                Add(ref writer, authoring.Locomotion);
            }
        }
    }
}
