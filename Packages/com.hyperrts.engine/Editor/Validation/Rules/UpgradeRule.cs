using HyperRTS.Simulation.Upgrades;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>An upgrade changes something, and its target lists hold prefabs.</summary>
    public sealed class UpgradeRule : AuthoringRule<UpgradeAuthoring>
    {
        protected override void Check(UpgradeAuthoring upgrade, ValidationIssues issues)
        {
            if (!upgrade.effects.Exists(effect => effect.bonuses.Count > 0))
            {
                issues.Warn(upgrade, "No stat bonuses: researching it changes nothing unless game code reacts to it.");
            }

            foreach (var effect in upgrade.effects)
            {
                CheckPrefabOptions(upgrade, effect.appliesTo, "Upgrade target", issues);
            }
        }
    }
}
