using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Veterancy;
using NUnit.Framework;
using UnityEditor;

namespace HyperRTS.Editor.Tests
{
    /// <summary>Validation rules for veterancy, fields, abilities and containers.</summary>
    public class GameplayRuleTests : TemplateFixture
    {
        [Test]
        public void FieldAffectingNobodyIsAnError()
        {
            var field = Track(EntityTemplates.Building()).AddComponent<AreaFieldAuthoring>();
            field.affects = FieldTargets.Units;

            Assert.IsTrue(ProjectValidator.Check(field).Any(issue => issue.Severity == MessageType.Error));
        }

        [Test]
        public void UnsortedVeterancyRanksAreReported()
        {
            var veterancy = Track(EntityTemplates.CombatUnit()).AddComponent<VeterancyAuthoring>();
            veterancy.ranks.Add(new VeterancyAuthoring.Rank { experience = 200f });
            veterancy.ranks.Add(new VeterancyAuthoring.Rank { experience = 100f });

            Assert.IsTrue(ProjectValidator.Check(veterancy).Any(issue => issue.Message.Contains("ascending")));
        }

        [Test]
        public void DuplicateAbilityNamesAreReported()
        {
            var abilities = Track(EntityTemplates.CombatUnit()).AddComponent<AbilityAuthoring>();
            abilities.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", damage = 1f });
            abilities.abilities.Add(new AbilityAuthoring.Entry { name = "Smoke", damage = 1f });

            Assert.IsTrue(ProjectValidator.Check(abilities).Any(issue => issue.Message.Contains("'Smoke'")));
        }

        [Test]
        public void OversizedPassengerLimitIsClampedByQuickFix()
        {
            var container = Track(EntityTemplates.Building()).AddComponent<ContainerAuthoring>();
            container.capacity = 2;
            container.maxPassengerSize = 4;

            var issue = ProjectValidator.Check(container).Single(found => found.Fix != null);
            issue.Fix();

            Assert.AreEqual(2, container.maxPassengerSize);
        }
    }
}
