using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Upgrades;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>An upgrade changes something: a stat bonus, or a weapon that needs it before it fires.</summary>
    public sealed class UpgradeRule : AuthoringRule<UpgradeAuthoring>
    {
        protected override void Check(UpgradeAuthoring upgrade, ValidationIssues issues)
        {
            if (!upgrade.effects.Exists(effect => effect.bonuses.Count > 0) && !UnlocksAWeapon(upgrade))
            {
                issues.Warn(upgrade, "No stat bonuses and no weapon needs it: researching it changes nothing unless game code reacts to it.");
            }
        }

        private static bool UnlocksAWeapon(UpgradeAuthoring upgrade) =>
            AssetSet.InProject().All<GameObject>()
                .SelectMany(prefab => prefab.GetComponentsInChildren<WeaponAuthoring>(true))
                .Any(weapons => weapons.extraWeapons.Exists(slot => slot.requires == upgrade));
    }
}
