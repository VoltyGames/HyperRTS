using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class ValidationTests : TemplateFixture
    {
        [Test]
        public void ProjectHasNoValidationErrors()
        {
            var errors = ProjectValidator.Run().Where(issue => issue.Severity == MessageType.Error);
            Assert.IsEmpty(errors.Select(issue => issue.ToString()));
        }

        [Test]
        public void SharedDisplayNamesAreReported()
        {
            var a = Track(EntityTemplates.Unit("Tank")).GetComponent<GameEntityAuthoring>();
            var b = Track(EntityTemplates.Unit("Tank")).GetComponent<GameEntityAuthoring>();
            var c = Track(EntityTemplates.Unit("Ranger")).GetComponent<GameEntityAuthoring>();

            var issues = ProjectValidator.DuplicateNames(new[] { a, b, c });

            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains("'Tank'", issues[0].Message);
        }

        [Test]
        public void SceneObjectAsProductionOptionIsReported()
        {
            var producer = Track(EntityTemplates.Producer()).GetComponent<ProducerAuthoring>();
            producer.productionOptions.Add(Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>());

            Assert.IsTrue(AuthoringChecks.For(producer).Any(issue => issue.Message.Contains("scene object")));
        }

        [Test]
        public void ResourceNodeWithoutTypeIsAnError()
        {
            var node = Track(EntityTemplates.ResourceNode()).GetComponent<ResourceNodeAuthoring>();

            Assert.IsTrue(AuthoringChecks.For(node).Any(issue => issue.Severity == MessageType.Error));
        }

        [TestCaseSource(nameof(Templates))]
        public void TemplatesAreValidApartFromSceneLocation(Func<GameObject> build)
        {
            var go = Track(build());
            var issues = go.GetComponents<MonoBehaviour>().SelectMany(AuthoringChecks.For)
                .Where(issue => !issue.Message.Contains("SubScene"));

            Assert.IsEmpty(issues.Select(issue => issue.Message));
        }

        private static IEnumerable<Func<GameObject>> Templates() => new Func<GameObject>[]
        {
            EntityTemplates.CombatUnit, EntityTemplates.Worker, EntityTemplates.Harvester, EntityTemplates.Producer,
            EntityTemplates.DropOff, EntityTemplates.DefenseTower,
        };
    }
}
