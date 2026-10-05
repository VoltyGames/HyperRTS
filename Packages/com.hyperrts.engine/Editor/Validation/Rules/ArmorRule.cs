using HyperRTS.Simulation.Combat;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Every armor entry names a damage type.</summary>
    public sealed class ArmorRule : AuthoringRule<ArmorAuthoring>
    {
        protected override void Check(ArmorAuthoring armor, ValidationIssues issues)
        {
            if (armor.modifiers.Exists(entry => entry.damageType == null))
            {
                issues.Warn(armor, "An armor entry has no damage type and will be ignored.");
            }
        }
    }
}
