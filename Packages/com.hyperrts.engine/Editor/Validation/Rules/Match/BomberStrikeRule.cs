using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Strikes;
using HyperRTS.Simulation.Vision.Detection;

namespace HyperRTS.Editor.Validation.Rules.Match
{
    /// <summary>A bomber strike names a stealthy aircraft, a bomb prefab and a charging point ability on its structure.</summary>
    public sealed class BomberStrikeRule : AuthoringRule<BomberStrikeAuthoring>
    {
        protected override void Check(BomberStrikeAuthoring strike, ValidationIssues issues)
        {
            CheckAircraft(strike, issues);
            if (strike.bomb == null)
            {
                issues.Error(strike, "Bomber strike has no bomb prefab.");
            }

            StrikeAbilityCheck.Run(strike, strike.ability, issues);
        }

        private static void CheckAircraft(BomberStrikeAuthoring strike, ValidationIssues issues)
        {
            if (strike.aircraft == null)
            {
                issues.Error(strike, "Bomber strike has no aircraft prefab.");
                return;
            }

            if (!strike.aircraft.TryGetComponent<FlightAuthoring>(out _))
            {
                issues.Error(strike, $"Aircraft '{strike.aircraft.name}' has no Flight.");
            }

            if (!strike.aircraft.TryGetComponent<StealthAuthoring>(out _))
            {
                issues.Warn(strike, $"Aircraft '{strike.aircraft.name}' has no Stealth, so it is detectable all the way in.");
            }
        }
    }
}
