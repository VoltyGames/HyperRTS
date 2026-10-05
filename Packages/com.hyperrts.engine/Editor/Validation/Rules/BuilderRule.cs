using HyperRTS.Simulation.Buildings;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Build options are prefab assets.</summary>
    public sealed class BuilderRule : AuthoringRule<BuilderAuthoring>
    {
        protected override void Check(BuilderAuthoring builder, ValidationIssues issues) =>
            CheckPrefabOptions(builder, builder.buildOptions, "Build option", issues);
    }
}
