using HyperRTS.Simulation.Fields;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>A field is named and affects at least one relation and one entity kind.</summary>
    public sealed class AreaFieldRule : AuthoringRule<AreaFieldAuthoring>
    {
        protected override void Check(AreaFieldAuthoring field, ValidationIssues issues)
        {
            if ((field.affects & FieldTargets.Everyone) == 0 || (field.affects & FieldTargets.AllKinds) == 0)
            {
                issues.Error(field, "Affects nobody: pick at least one of Own/Allies/Enemies/Neutral and one of Units/Buildings.");
            }

            if (string.IsNullOrWhiteSpace(field.fieldName))
            {
                issues.Warn(field, "No field name: every unnamed field counts as one type and won't stack with the others.");
            }
        }
    }
}
