using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Editor.Validation.Rules;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Production;
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

            var issues = new ValidationIssues();
            new DuplicateNameRule().Check(new PrefabSet(new[] { a.gameObject, b.gameObject, c.gameObject }), issues);

            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains("'Tank'", issues[0].Message);
        }

        [Test]
        public void SceneObjectAsProductionOptionIsReported()
        {
            var producer = Track(EntityTemplates.Producer()).GetComponent<ProducerAuthoring>();
            producer.productionOptions.Add(Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>());

            Assert.IsTrue(ProjectValidator.Check(producer).Any(issue => issue.Message.Contains("scene object")));
        }

        [Test]
        public void ResourceNodeWithoutTypeIsAnError()
        {
            var node = Track(EntityTemplates.ResourceNode()).GetComponent<ResourceNodeAuthoring>();

            Assert.IsTrue(ProjectValidator.Check(node).Any(issue => issue.Severity == MessageType.Error));
        }

        [Test]
        public void MissingRequiredAuthoringIsReported()
        {
            var harvester = Track(EntityTemplates.Building()).AddComponent<HarvesterAuthoring>();

            Assert.IsTrue(ProjectValidator.Check(harvester).Any(issue => issue.Message.Contains("needs a Unit")));
        }

        [Test]
        public void SceneObjectAsDeathSpawnIsReported()
        {
            var unit = Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
            unit.deathSpawn = Track(EntityTemplates.Unit("Wreck"));

            Assert.IsTrue(ProjectValidator.Check(unit).Any(issue => issue.Message.StartsWith("Death spawn 'Wreck'")));
        }

        [Test]
        public void ArmedDeliveryCarrierIsAnError()
        {
            var source = Track(EntityTemplates.Building()).AddComponent<DeliverySourceAuthoring>();
            source.carrier = Track(EntityTemplates.CombatUnit()).GetComponent<UnitAuthoring>();

            var issues = ProjectValidator.Check(source);

            Assert.IsTrue(issues.Any(issue => issue.Severity == MessageType.Error && issue.Message.Contains("unarmed")));
        }

        [Test]
        public void ModelWithoutTeamColorMaterialIsReported()
        {
            var unit = Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
            var plain = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            unit.GetComponentInChildren<Renderer>().sharedMaterial = plain;

            var reported = ProjectValidator.Check(unit).Any(issue => issue.Message.Contains("owner's colour"));
            UnityEngine.Object.DestroyImmediate(plain);

            Assert.IsTrue(reported);
        }

        [TestCaseSource(nameof(Templates))]
        public void TemplatesAreValidApartFromSceneLocation(Func<GameObject> build)
        {
            var go = Track(build());
            var issues = go.GetComponents<MonoBehaviour>().SelectMany(ProjectValidator.Check)
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
