using System;
using System.Collections.Generic;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Rules.Effects;
using HyperRTS.Simulation.Destruction;

namespace HyperRTS.Editor.Validation.Rules.Buildings
{
    /// <summary>Sections have a valid effect rig, complete intact and broken pairs, materials, a way to the ground, and each part and weapon mount in one section.</summary>
    public sealed class SectionsRule : AuthoringRule<SectionsAuthoring>
    {
        private static void CheckOneSection<T>(SectionsAuthoring sections, string what, Func<SectionEntry, IEnumerable<T>> carried,
            Func<T, string> label, ValidationIssues issues)
        {
            var owners = new Dictionary<T, string>();
            foreach (var entry in sections.sections)
            {
                foreach (var item in carried(entry) ?? Array.Empty<T>())
                {
                    if (item is null)
                    {
                        continue;
                    }

                    var section = entry.intact != null ? entry.intact.name : "(no intact model)";
                    if (owners.TryGetValue(item, out var first) && first != section)
                    {
                        issues.Error(sections, $"The {what} '{label(item)}' on '{sections.name}' belongs to sections '{first}' and '{section}'; it needs one.");
                    }

                    owners[item] = section;
                }
            }
        }

        protected override void Check(SectionsAuthoring sections, ValidationIssues issues)
        {
            EffectRigCheck.Check(sections, sections.effect, "Sections effect", true, issues);
            foreach (var problem in sections.Problems())
            {
                issues.Error(sections, problem);
            }

            CheckOneSection(sections, "part", entry => entry.parts, part => part.name, issues);
            CheckOneSection(sections, "weapon mount", entry => entry.weapons, mount => mount.ToString(), issues);
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
