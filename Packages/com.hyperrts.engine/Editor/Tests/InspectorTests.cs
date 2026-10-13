using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Templates;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;
using UnityEditor;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Inspector helpers: issue badge text, [ShowIf] conditions, and entity and production option summaries.</summary>
    public class InspectorTests : TemplateFixture
    {
        [TestCase(0, "")]
        [TestCase(1, "1 error")]
        [TestCase(3, "3 errors")]
        public void BadgeCountsArePlural(int count, string expected) =>
            Assert.AreEqual(expected, IssueBadge.CountText(count, "error"));

        [Test]
        public void ShowIfFollowsNumbersAndObjects()
        {
            var weapon = Track(EntityTemplates.CombatUnit()).GetComponent<WeaponAuthoring>();
            var serialized = new SerializedObject(weapon);
            Assert.IsFalse(ConditionalFields.IsMet(serialized.FindProperty(nameof(WeaponAuthoring.splashRadius))));
            Assert.IsFalse(ConditionalFields.IsMet(serialized.FindProperty(nameof(WeaponAuthoring.projectilePrefab))));

            weapon.splashRadius = 2f;
            weapon.projectilePrefab = weapon.gameObject;
            serialized.Update();
            Assert.IsTrue(ConditionalFields.IsMet(serialized.FindProperty(nameof(WeaponAuthoring.splashRadius))));
            Assert.IsTrue(ConditionalFields.IsMet(serialized.FindProperty(nameof(WeaponAuthoring.projectilePrefab))));
        }

        [Test]
        public void ShowIfRejectsUnsupportedConditions()
        {
            var weapon = Track(EntityTemplates.CombatUnit()).GetComponent<WeaponAuthoring>();
            var serialized = new SerializedObject(weapon);
            Assert.Throws<System.ArgumentException>(() =>
                ConditionalFields.IsMet(serialized.FindProperty(nameof(WeaponAuthoring.targets))));
        }

        [Test]
        public void OptionSummaryShowsCostAndTime()
        {
            var upgrade = Track(EntityTemplates.Upgrade()).GetComponent<UpgradeAuthoring>();
            upgrade.researchTime = 30f;
            Assert.AreEqual("free · 30s", EntitySummary.ProducibleText(upgrade));
            Assert.AreEqual("", EntitySummary.ProducibleText(null));
        }

        [Test]
        public void SummaryListsRoleModulesDamageAndCost()
        {
            var soldier = Track(EntityTemplates.CombatUnit()).GetComponent<GameEntityAuthoring>();

            Assert.AreEqual("Unit · Weapon · 10 DPS · free", EntitySummary.Line(soldier));
        }
    }
}
