using HyperRTS.Simulation.Effects;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Each set rig is a valid effect rig; a fire rig needs a muzzle to play from.</summary>
    public sealed class EntityEffectsRule : AuthoringRule<EntityEffectsAuthoring>
    {
        protected override void Check(EntityEffectsAuthoring effects, ValidationIssues issues)
        {
            if (effects.fire != null)
            {
                EffectRigCheck.Check(effects, effects.fire, "Fire", issues);
                if (effects.muzzle == null)
                {
                    issues.Error(effects, $"Fire '{effects.fire.name}' has no muzzle socket.");
                }
            }

            CheckIfSet(effects, effects.death, "Death", issues);
            CheckIfSet(effects, effects.ability, "Ability", issues);
        }

        private static void CheckIfSet(EntityEffectsAuthoring effects, GameObject rig, string field,
            ValidationIssues issues)
        {
            if (rig != null)
            {
                EffectRigCheck.Check(effects, rig, field, issues);
            }
        }
    }
}
