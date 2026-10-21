using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// Gives a ground vehicle handling: acceleration, braking, turning and reversing. Without it a unit steps
    /// straight at full speed, as infantry do.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Units + "Locomotion")]
    [Icon(HyperRTSIcons.Units)]
    [HelpURL(HyperRTSDocs.Units)]
    [DisallowMultipleComponent]
    [RequiresAuthoring(typeof(UnitAuthoring), "a Unit")]
    public class LocomotionAuthoring : AuthoringBehaviour
    {
        [Tooltip("Speed gained per second, in m/s2.")]
        [Min(0.1f)]
        public float acceleration = 2.5f;

        [Tooltip("Speed shed per second when braking, in m/s2.")]
        [Min(0.1f)]
        public float braking = 6f;

        [Tooltip("Fastest the hull turns, in degrees per second.")]
        [Min(1f)]
        public float turnRate = 50f;

        [Tooltip("Tightest circle it drives, in metres. 0 turns at any speed. Ignored when it pivots.")]
        [Min(0f)]
        public float turnRadius;

        [Tooltip("Stops and turns in place when far off its heading, as tracked vehicles do.")]
        public bool pivots = true;

        [Tooltip("Top speed in reverse, in m/s.")]
        [Min(0.1f)]
        public float reverseSpeed = 2.5f;

        [Tooltip("Backs up to a goal at most this far behind, in metres, when turning round would clearly take longer.")]
        [Min(0f)]
        public float reverseDistance = 10f;

        public Locomotion Locomotion => new()
        {
            Acceleration = acceleration,
            Braking = braking,
            TurnRate = math.radians(turnRate),
            TurnRadius = turnRadius,
            Pivots = pivots,
            ReverseSpeed = reverseSpeed,
            ReverseDistance = reverseDistance,
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
