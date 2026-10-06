using HyperRTS.Simulation.Veterancy;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Veterancy has ranks, in ascending order of experience.</summary>
    public sealed class VeterancyRule : AuthoringRule<VeterancyAuthoring>
    {
        protected override void Check(VeterancyAuthoring veterancy, ValidationIssues issues)
        {
            if (veterancy.ranks.Count == 0)
            {
                issues.Warn(veterancy, "No ranks: the unit earns experience but never ranks up.");
                return;
            }

            for (var i = 1; i < veterancy.ranks.Count; i++)
            {
                if (veterancy.ranks[i].experience <= veterancy.ranks[i - 1].experience)
                {
                    issues.Warn(veterancy, $"Rank {i + 1} needs no more experience than rank {i}; list ranks in ascending order.");
                    return;
                }
            }
        }
    }
}
