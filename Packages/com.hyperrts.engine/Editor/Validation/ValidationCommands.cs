using HyperRTS.Editor.Validation.Issues;
using Unity.Pipeline.Commands;

namespace HyperRTS.Editor.Validation
{
    /// <summary>The Validate page as a pipeline command.</summary>
    public static class ValidationCommands
    {
        [CliCommand("project", "validate", "Run every validation rule over the project's prefabs and the open scenes: counts, then each issue with its severity, message, asset path and fix label.", Tags = new[] { "hyperrts/validate" })]
        public static ValidationReport Validate() => ValidationReport.From(ProjectValidator.Run());
    }
}
