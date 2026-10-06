using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Match;
using Unity.NetCode;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Clients need physics running to click on units, but Netcode only runs it for predicted ghosts.</summary>
    public sealed class MatchPhysicsRule : AuthoringRule<MatchAuthoring>
    {
        protected override void Check(MatchAuthoring match, ValidationIssues issues)
        {
            var hasConfig = match.TryGetComponent<NetCodePhysicsConfig>(out var config);
            if (hasConfig && config.PhysicGroupRunMode == PhysicGroupRunMode.AlwaysRun)
            {
                return;
            }

            issues.Warn(match, "Clients can't click units: Netcode only runs physics for predicted ghosts.",
                "Always Run Physics", () =>
                {
                    var target = hasConfig
                        ? config
                        : (NetCodePhysicsConfig)QuickFixes.AddComponent(match.gameObject, typeof(NetCodePhysicsConfig));
                    EditorUndo.Record(target, "Always Run Physics",
                        () => target.PhysicGroupRunMode = PhysicGroupRunMode.AlwaysRun);
                });
        }
    }
}
