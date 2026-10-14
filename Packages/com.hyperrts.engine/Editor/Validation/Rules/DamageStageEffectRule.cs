using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Each damage stage's effect, when set, is a valid effect rig.</summary>
    public sealed class DamageStageEffectRule : AuthoringRule<DamageStagesAuthoring>
    {
        protected override void Check(DamageStagesAuthoring damage, ValidationIssues issues)
        {
            for (var i = 0; i < damage.stages.Count; i++)
            {
                EffectRigCheck.CheckOptional(damage, damage.stages[i].effect, $"Damage stage {i} effect", issues);
            }
        }
    }
}
