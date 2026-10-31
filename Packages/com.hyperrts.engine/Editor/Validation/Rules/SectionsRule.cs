using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Sections have a valid effect rig, complete intact and broken pairs, materials, and a way to the ground.</summary>
    public sealed class SectionsRule : AuthoringRule<SectionsAuthoring>
    {
        protected override void Check(SectionsAuthoring sections, ValidationIssues issues)
        {
            EffectRigCheck.Check(sections, sections.effect, "Sections effect", issues);
            foreach (var problem in sections.Problems())
            {
                issues.Error(sections, problem);
            }

            var anchored = false;
            foreach (var entry in sections.sections)
            {
                anchored |= entry.anchored;
                if (!entry.anchored && (entry.supports == null || entry.supports.Length == 0) && entry.intact != null)
                {
                    issues.Error(sections, $"Section '{entry.intact.name}' on '{sections.name}' rests on nothing and falls at once.");
                }
            }

            if (sections.sections.Length > 0 && !anchored)
            {
                issues.Error(sections, $"No section of '{sections.name}' stands on the ground, so the whole building falls.");
            }
        }
    }
}
