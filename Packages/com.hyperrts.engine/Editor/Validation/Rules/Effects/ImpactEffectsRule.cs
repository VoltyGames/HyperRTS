using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Cues.Effects;
using System.Linq;

namespace HyperRTS.Editor.Validation.Rules.Effects
{
    /// <summary>An impact has marks, or effects with valid surface rigs.</summary>
    public sealed class ImpactEffectsRule : AuthoringRule<ImpactAuthoring>
    {
        protected override void Check(ImpactAuthoring impacts, ValidationIssues issues)
        {
            if (impacts.effects != null || !impacts.marks.Any(mark => mark != null))
            {
                EffectRigCheck.CheckSurfaceEffects(impacts, impacts.effects, "Impact effects", issues);
            }
        }
    }
}
