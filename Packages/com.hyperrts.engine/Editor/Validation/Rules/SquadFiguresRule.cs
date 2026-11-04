using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Squads;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A squad has a figure for each of its members, and each figure shows at most one other model: an upgraded one or an emplaced one, not both.</summary>
    public sealed class SquadFiguresRule : AuthoringRule<SquadFiguresAuthoring>
    {
        protected override void Check(SquadFiguresAuthoring squad, ValidationIssues issues)
        {
            var members = squad.TryGetComponent<SquadAuthoring>(out var authoring) ? authoring.members.Count : 0;
            if (members != squad.figures.Count)
            {
                issues.Error(squad, $"{squad.name} lists {squad.figures.Count} figures but has {members} squad members.");
            }

            for (var i = 0; i < squad.figures.Count; i++)
            {
                if (squad.figures[i].HasTwoVariants)
                {
                    issues.Error(squad, $"{squad.name} figure {i} has both an upgraded and an emplaced figure; keep one.");
                }
            }
        }
    }
}
