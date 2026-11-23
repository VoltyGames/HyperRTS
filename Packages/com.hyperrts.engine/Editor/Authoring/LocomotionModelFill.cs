using System;
using System.Linq;
using HyperRTS.Simulation.Movement;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>
    /// A wheeled Locomotion's wheelbase and rear axle from its Wheel_L01... wheels: the first steering axles from the
    /// front steer and the rest make the rear axle. Tracks take 0 for both.
    /// </summary>
    internal sealed class LocomotionModelFill : ModelFill<LocomotionAuthoring>
    {
        protected override void Fill(LocomotionAuthoring locomotion)
        {
            if (!locomotion.Chassis.IsWheeled)
            {
                locomotion.wheelbase = 0f;
                locomotion.rearAxle = 0f;
                return;
            }

            var axles = locomotion.Axles();
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
    }
}
