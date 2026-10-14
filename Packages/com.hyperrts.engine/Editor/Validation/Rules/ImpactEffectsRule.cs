using HyperRTS.Simulation.Cues;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Impacts need valid surface rigs.</summary>
    public sealed class ImpactEffectsRule : AuthoringRule<ImpactEffectsAuthoring>
    {
        protected override void Check(ImpactEffectsAuthoring impacts, ValidationIssues issues) =>
            EffectRigCheck.CheckSurfaceEffects(impacts, impacts.effects, "Impact effects", issues);
    }
}
