using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Rules.Fields;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Upgrades;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests.Validation.Rules.Fields
{
    /// <summary>The entity rule's prerequisite warning, past the broken case every rule has.</summary>
    public class GameEntityRuleTests : TemplateFixture
    {
        [Test]
        public void GameEntityRule_WarnsOnUnitPrerequisites_NotOnResearch()
        {
            var unit = Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
            unit.prerequisites.Add(Track(EntityTemplates.Upgrade()).GetComponent<UpgradeAuthoring>());
            Assert.IsFalse(PrerequisiteWarned(unit), "research is a prerequisite a player can finish");

            unit.prerequisites.Add(Track(EntityTemplates.Unit("Tank")).GetComponent<UnitAuthoring>());
            Assert.IsTrue(PrerequisiteWarned(unit));
        }

        private static bool PrerequisiteWarned(UnitAuthoring unit) =>
            Issues(unit).Any(message => message.Contains("unit prerequisite"));

        private static string[] Issues(UnitAuthoring unit)
        {
            var issues = new ValidationIssues();
            ((IAuthoringRule)new GameEntityRule()).Check(unit, issues);
            return issues.Select(issue => issue.Message).ToArray();
        }
    }
}
