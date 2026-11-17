using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Simulation.Abilities;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Match
{
    /// <summary>A strike's ability is a point-targeted entry on its structure that starts on cooldown.</summary>
    public static class StrikeAbilityCheck
    {
        public static void Run(Component strike, string ability, ValidationIssues issues)
        {
            if (!strike.TryGetComponent<AbilityAuthoring>(out var abilities))
            {
                return;
            }

            var entry = abilities.abilities.Find(each => each.name == ability);
            if (entry == null)
            {
                issues.Error(strike, $"No Ability entry named '{ability}' on this structure.");
                return;
            }

            if (entry.target != AbilityTarget.Point || !entry.startsOnCooldown)
            {
                issues.Error(strike, $"Ability '{ability}' must target a point and start on cooldown.");
            }
        }
    }
}
