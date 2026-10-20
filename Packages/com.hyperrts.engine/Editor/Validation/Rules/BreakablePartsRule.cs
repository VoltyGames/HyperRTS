using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Breakable parts have a valid effect rig and complete intact and broken pairs.</summary>
    public sealed class BreakablePartsRule : AuthoringRule<BreakablePartsAuthoring>
    {
        protected override void Check(BreakablePartsAuthoring parts, ValidationIssues issues)
        {
            EffectRigCheck.Check(parts, parts.effect, "Breakable parts effect", issues);
            foreach (var problem in parts.Problems())
            {
                issues.Error(parts, problem);
            }
        }
    }
}
