using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Abilities;
using UnityEditor;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Ability names are unique per entity and spawn prefabs are prefab assets.</summary>
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

                if (ability.spawnPrefab != null && !PrefabUtility.IsPartOfPrefabAsset(ability.spawnPrefab))
                {
                    issues.Warn(authoring, $"'{ability.name}' spawns a scene object; reference the prefab asset.");
                }
            }

            if (authoring.abilities.Any(ability => ability.spawnPrefab == null && ability.damage == 0f))
            {
                issues.Info(authoring, "An ability without built-in effects only raises its event for game code.");
            }
        }
    }
}
