using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Death;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>A turret toss names a hull and a turret, and the turret carries the throw clients show.</summary>
    public sealed class TurretTossRule : AuthoringRule<TurretTossAuthoring>
    {
        protected override void Check(TurretTossAuthoring toss, ValidationIssues issues)
        {
            if (toss.hull == null || toss.turret == null)
            {
                issues.Error(toss, "Needs a hull and a turret.");
                return;
            }

            if (!toss.turret.TryGetComponent<TossedTurretAuthoring>(out _))
            {
                issues.Error(toss, $"Turret '{toss.turret.name}' needs a Tossed Turret component to fly on clients.");
            }
        }
    }
}
