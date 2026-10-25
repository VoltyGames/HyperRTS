using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Templates;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using System.Linq;
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
            var splash = Stat(nameof(WeaponStats.splashRadius));
            var projectile = Stat(nameof(WeaponStats.projectilePrefab));
            Assert.IsFalse(ConditionalFields.IsMet(serialized.FindProperty(splash)));
            Assert.IsFalse(ConditionalFields.IsMet(serialized.FindProperty(projectile)));

            weapon.stats.splashRadius = 2f;
            weapon.stats.projectilePrefab = weapon.gameObject;
            serialized.Update();
            Assert.IsTrue(ConditionalFields.IsMet(serialized.FindProperty(splash)));
            Assert.IsTrue(ConditionalFields.IsMet(serialized.FindProperty(projectile)));
        }

        [Test]
        public void ShowIfRejectsUnsupportedConditions()
        {
            var weapon = Track(EntityTemplates.CombatUnit()).GetComponent<WeaponAuthoring>();
            var serialized = new SerializedObject(weapon);
            Assert.Throws<System.ArgumentException>(() =>
                ConditionalFields.IsMet(serialized.FindProperty(Stat(nameof(WeaponStats.targets)))));
        }

        [Test]
        public void ShowIfReadsSiblingsInsideNestedStats() =>
            CollectionAssert.Contains(ConditionalFields.Conditions(typeof(WeaponAuthoring)).ToList(),
                (Stat(nameof(WeaponStats.splashEdgeDamage)), Stat(nameof(WeaponStats.splashRadius))));

        [Test]
        public void OptionSummaryShowsCostAndTime()
        {
            var upgrade = Track(EntityTemplates.Upgrade()).GetComponent<UpgradeAuthoring>();
            upgrade.buildTime = 30f;
            Assert.AreEqual("free · 30s", EntitySummary.ProducibleText(upgrade));
            Assert.AreEqual("", EntitySummary.ProducibleText(null));
        }

        [Test]
        public void SummaryListsRoleModulesDamageAndCost()
        {
            var soldier = Track(EntityTemplates.CombatUnit()).GetComponent<GameEntityAuthoring>();

            Assert.AreEqual("Unit · Weapon · 10 DPS · free", EntitySummary.Line(soldier));
        }

        private static string Stat(string field) => $"{nameof(WeaponAuthoring.stats)}.{field}";
    }
}
