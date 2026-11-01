using HyperRTS.Simulation.Auras;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>An aura is named and affects at least one relation and one entity kind.</summary>
    public sealed class AuraRule : AuthoringRule<AuraAuthoring>
    {
        protected override void Check(AuraAuthoring aura, ValidationIssues issues)
        {
            if ((aura.affects & AuraTargets.Everyone) == 0 || (aura.affects & AuraTargets.AllKinds) == 0)
            {
                issues.Error(aura, "Affects nobody: pick at least one of Own/Allies/Enemies/Neutral and one of Units/Buildings.");
            }

            if (string.IsNullOrWhiteSpace(aura.auraName))
            {
                issues.Warn(aura, "No aura name: every unnamed aura counts as one type and won't stack with the others.");
            }
        }
    }
}
