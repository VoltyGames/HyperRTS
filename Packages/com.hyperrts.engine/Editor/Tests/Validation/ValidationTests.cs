using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Auras;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Resources.Gathering;
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
        [TestCase("Aura", "Affects nobody")]
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
                case "Aura":
                    var field = Track(EntityTemplates.Building()).AddComponent<AuraAuthoring>();
                    field.affects = AuraTargets.Units;
                    return field;
                default:
                    var source = Track(EntityTemplates.Building()).AddComponent<DeliverySourceAuthoring>();
                    source.carrier = Track(EntityTemplates.CombatUnit()).GetComponent<UnitAuthoring>();
                    return source;
            }
        }

        // A variant inherits its base's cause, so the base lists the issue once, "and 1 variant"; a variant's own issue stays.
        [Test]
        public void VariantsShareTheBasePrefabsIssue()
        {
            const string folder = "Assets/_ValidationGroupTests";
            AssetDatabase.CreateFolder("Assets", "_ValidationGroupTests");
            try
            {
                var template = Track(EntityTemplates.Unit());
                var basePrefab = PrefabUtility.SaveAsPrefabAsset(template, folder + "/Base.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                var variant = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Variant.prefab");
                UnityEngine.Object.DestroyImmediate(instance);
                var issues = new ValidationIssues();
                issues.Warn(variant.GetComponent<UnitAuthoring>(), "No renderer");
                issues.Warn(basePrefab.GetComponent<UnitAuthoring>(), "No renderer");
                issues.Warn(variant.GetComponent<UnitAuthoring>(), "Own problem");

                var groups = issues.GroupVariants();

                Assert.AreEqual(2, groups.Count);
                Assert.AreEqual("and 1 variant", groups[0].VariantText);
                Assert.AreEqual("Own problem", groups[1].Issue.Message);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static IEnumerable<Func<GameObject>> Templates() => new Func<GameObject>[]
        {
            EntityTemplates.CombatUnit, EntityTemplates.Worker, EntityTemplates.Harvester, EntityTemplates.Producer,
            EntityTemplates.DropOff, EntityTemplates.DefenseTower,
        };
    }
}
