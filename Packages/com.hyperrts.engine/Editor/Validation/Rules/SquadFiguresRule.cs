using HyperRTS.Presentation.Animation;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A figure shows at most one other model: an upgraded one or an emplaced one, not both.</summary>
    public sealed class SquadFiguresRule : AuthoringRule<SquadFiguresAuthoring>
    {
        protected override void Check(SquadFiguresAuthoring squad, ValidationIssues issues)
        {
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
