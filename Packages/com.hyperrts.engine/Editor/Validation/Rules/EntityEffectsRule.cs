using HyperRTS.Simulation.Cues;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Each set rig is a valid effect rig; a fire rig needs a muzzle to play from.</summary>
    public sealed class EntityEffectsRule : AuthoringRule<EntityEffectsAuthoring>
    {
        protected override void Check(EntityEffectsAuthoring effects, ValidationIssues issues)
        {
            EffectRigCheck.CheckOptional(effects, effects.fire, "Fire", issues);
            if (effects.fire != null && effects.muzzle == null)
            {
                issues.Error(effects, $"Fire '{effects.fire.name}' has no muzzle socket.");
            }

            EffectRigCheck.CheckOptional(effects, effects.death, "Death", issues);
            EffectRigCheck.CheckOptional(effects, effects.ability, "Ability", issues);
        }
    }
}
