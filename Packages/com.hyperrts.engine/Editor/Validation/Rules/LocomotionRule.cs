using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A wheeled unit (no pivots, a turn radius) needs a wheelbase and both axles inside its hull.</summary>
    public sealed class LocomotionRule : AuthoringRule<LocomotionAuthoring>
    {
        protected override void Check(LocomotionAuthoring locomotion, ValidationIssues issues)
        {
            if (!locomotion.Chassis.IsWheeled)
            {
                return;
            }

            if (locomotion.wheelbase <= 0f)
            {
                issues.Error(locomotion, $"Wheeled '{locomotion.name}' has no wheelbase; use Fill From Wheels.");
                return;
            }

            var unit = locomotion.GetComponent<UnitAuthoring>();
            var halfLength = new NavAgent { Radius = unit.radius, HullLength = unit.hullLength }.HalfLength;
            var front = locomotion.wheelbase - locomotion.rearAxle;
            if (locomotion.rearAxle >= halfLength || front >= halfLength)
            {
                issues.Error(locomotion, $"'{locomotion.name}' has axles outside its {halfLength * 2f:0.#} m hull; " +
                                         "check its wheelbase and rear axle.");
            }
        }
    }
}
