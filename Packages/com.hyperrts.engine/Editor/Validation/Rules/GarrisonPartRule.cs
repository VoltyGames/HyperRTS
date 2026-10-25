using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Transport;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A garrison part sits under a container and names the passengers that show it.</summary>
    public sealed class GarrisonPartRule : AuthoringRule<GarrisonPartAuthoring>
    {
        protected override void Check(GarrisonPartAuthoring part, ValidationIssues issues)
        {
            // A hidden part may be saved inactive, which the default lookup skips.
            if (part.GetComponentInParent<ContainerAuthoring>(true) == null)
            {
                issues.Error(part, $"'{part.name}' has no Container above it, so nothing ever shows it.");
            }

            CheckEmptyEntries(part, part.shownFor, unit => unit == null, "A garrison part entry names no passenger.", issues);
            if (part.shownFor.Count == 0)
            {
                issues.Error(part, $"'{part.name}' lists no passengers, so it never shows.");
            }
        }
    }
}
