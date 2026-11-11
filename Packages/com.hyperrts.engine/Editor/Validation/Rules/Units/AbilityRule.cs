using System.Collections.Generic;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Abilities;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>Ability names are unique per entity.</summary>
    public sealed class AbilityRule : AuthoringRule<AbilityAuthoring>
    {
        protected override void Check(AbilityAuthoring authoring, ValidationIssues issues)
        {
            var names = new HashSet<string>();
            foreach (var ability in authoring.abilities)
            {
                if (!names.Add(ability.name))
                {
                    issues.Warn(authoring, $"Two abilities are named '{ability.name}'; the name is the ability's id.");
                }
            }
        }
    }
}
