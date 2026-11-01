using HyperRTS.Simulation.Combat.Damage;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Every armor entry names a damage type.</summary>
    public sealed class ArmorRule : AuthoringRule<ArmorAuthoring>
    {
        protected override void Check(ArmorAuthoring armor, ValidationIssues issues)
        {
            CheckEmptyEntries(armor, armor.modifiers, entry => entry.damageType == null,
                "An armor entry has no damage type and will be ignored.", issues);
        }
    }
}
