using HyperRTS.Simulation.Effects;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>The trail streams a valid effect rig.</summary>
    public sealed class EffectTrailRule : AuthoringRule<EffectTrailAuthoring>
    {
        protected override void Check(EffectTrailAuthoring trail, ValidationIssues issues) =>
            EffectRigCheck.Check(trail, trail.rig, "Trail rig", issues);
    }
}
