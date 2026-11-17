using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Strikes;

namespace HyperRTS.Editor.Validation.Rules.Match
{
    /// <summary>A missile strike names a missile prefab and a charging point ability on its structure.</summary>
    public sealed class MissileStrikeRule : AuthoringRule<MissileStrikeAuthoring>
    {
        protected override void Check(MissileStrikeAuthoring strike, ValidationIssues issues)
        {
            if (strike.missile == null)
            {
                issues.Error(strike, "Missile strike has no missile prefab.");
            }

            if (strike.blast.coreRadius > strike.blast.splashRadius)
            {
                issues.Error(strike, "The blast's core radius is wider than its splash radius.");
            }

            StrikeAbilityCheck.Run(strike, strike.ability, issues);
        }
    }
}
