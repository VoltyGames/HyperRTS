using HyperRTS.Simulation.Combat.Death;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A turret toss spawns ghosts, and the thrown turret carries the throw clients show.</summary>
    public sealed class TurretTossRule : AuthoringRule<TurretTossAuthoring>
    {
        protected override void Check(TurretTossAuthoring toss, ValidationIssues issues)
        {
            if (toss.hull == null || toss.turret == null)
            {
                issues.Error(toss, "Needs a hull and a turret.");
                return;
            }

            WarnIfNotGhost(toss, toss.hull, $"Hull '{toss.hull.name}' is not a ghost, so clients won't see the wreck.", issues);
            WarnIfNotGhost(toss, toss.turret, $"Turret '{toss.turret.name}' is not a ghost, so clients won't see it land.", issues);
            if (!toss.turret.TryGetComponent<TossedTurretAuthoring>(out _))
            {
                issues.Error(toss, $"Turret '{toss.turret.name}' needs a Tossed Turret component to fly on clients.");
            }
        }
    }
}
