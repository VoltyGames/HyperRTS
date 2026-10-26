using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using HyperRTS.Simulation.Veterancy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>The project passes validation, templates start valid, and the broken cases that must block are errors.</summary>
    public class ValidationTests : TemplateFixture
    {
        [Test]
        public void ProjectHasNoValidationErrors()
        {
            var errors = ProjectValidator.Run().Where(issue => issue.Severity == MessageType.Error);
            Assert.IsEmpty(errors.Select(issue => issue.ToString()));
        }

        [Test]
        public void EngineShadersCompileWithoutErrors() =>
            Assert.IsEmpty(ShaderChecks.Broken("Packages/com.hyperrts.engine"));

        [TestCaseSource(nameof(Templates))]
        public void TemplatesAreValidApartFromSceneLocation(Func<GameObject> build)
        {
            var go = Track(build());
            var issues = go.GetComponents<MonoBehaviour>().SelectMany(ProjectValidator.Check)
                .Where(issue => !issue.Message.Contains("SubScene"));

            Assert.IsEmpty(issues.Select(issue => issue.Message));
        }

        // RuleFiresTests checks the message; these cases must also block the project.
        [TestCase("ResourceNode", "no resource type")]
        [TestCase("AreaField", "Affects nobody")]
        [TestCase("ArmedCarrier", "unarmed")]
        public void BrokenCaseIsAnError(string broken, string message)
        {
            var issues = ProjectValidator.Check(Build(broken));

            Assert.IsTrue(issues.Any(issue => issue.Severity == MessageType.Error && issue.Message.Contains(message)),
                string.Join(" | ", issues.Select(issue => issue.Message)));
        }

        [Test]
        public void UnsortedVeterancyRanksAreReported()
        {
            var veterancy = Track(EntityTemplates.CombatUnit()).AddComponent<VeterancyAuthoring>();
            veterancy.ranks.Add(new VeterancyAuthoring.Rank { experience = 200f });
            veterancy.ranks.Add(new VeterancyAuthoring.Rank { experience = 100f });

            Assert.IsTrue(ProjectValidator.Check(veterancy).Any(issue => issue.Message.Contains("ascending")));
        }

        private Component Build(string broken)
        {
            switch (broken)
            {
                case "ResourceNode":
                    return Track(EntityTemplates.ResourceNode()).GetComponent<ResourceNodeAuthoring>();
                case "AreaField":
                    var field = Track(EntityTemplates.Building()).AddComponent<AreaFieldAuthoring>();
                    field.affects = FieldTargets.Units;
                    return field;
                default:
                    var source = Track(EntityTemplates.Building()).AddComponent<DeliverySourceAuthoring>();
                    source.carrier = Track(EntityTemplates.CombatUnit()).GetComponent<UnitAuthoring>();
                    return source;
            }
        }

        private static IEnumerable<Func<GameObject>> Templates() => new Func<GameObject>[]
        {
            EntityTemplates.CombatUnit, EntityTemplates.Worker, EntityTemplates.Harvester, EntityTemplates.Producer,
            EntityTemplates.DropOff, EntityTemplates.DefenseTower,
        };
    }
}
