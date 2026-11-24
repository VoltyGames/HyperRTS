using System;
using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Simulation.GameEntities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Tests.Tools
{
    /// <summary>The New Entity wizard's choices: a project base becomes a named variant, and every template is offered.</summary>
    public class EntityWizardTests : TemplateFixture
    {
        private const string Folder = "Assets/_EntityWizardTests";

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void BaseBecomesANamedVariant()
        {
            AssetDatabase.CreateFolder("Assets", "_EntityWizardTests");
            var template = Track(EntityTemplates.CombatUnit());
            var basePrefab = PrefabUtility.SaveAsPrefabAsset(template, Folder + "/Tank_Base.prefab");
            var bases = ScriptableObject.CreateInstance<EntityBases>();
            bases.groups.Add(new EntityBaseGroup { name = "Wizard test units", prefabs = { basePrefab } });
            AssetDatabase.CreateAsset(bases, Folder + "/EntityBases.asset");

            var choice = EntityChoice.All().Single(group => group.Heading.Label == "Wizard test units").Choices.Single();
            var built = Track(choice.Build("USA_M1A2"));
            var saved = PrefabUtility.SaveAsPrefabAsset(built, Folder + "/USA_M1A2.prefab");

            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(saved));
            Assert.AreEqual(basePrefab, PrefabUtility.GetCorrespondingObjectFromSource(saved));
            Assert.AreEqual("USA M1A2", saved.GetComponent<GameEntityAuthoring>().displayName);
        }

        [Test]
        public void StepRunsOnTheSavedPrefab()
        {
            AssetDatabase.CreateFolder("Assets", "_EntityWizardTests");
            var choice = EntityChoice.All().Single(group => group.Heading.Label == "Templates").Choices.First();
            var steps = EntityWizardStep.For(Folder);
            Assert.That(steps.Select(step => step.GetType()), Is.EqualTo(new[] { typeof(RecordingStep) }));

            var prefab = choice.Save("Stepped", Folder, steps);

            Assert.AreEqual(prefab, ((RecordingStep)steps.Single()).Seen);
            Assert.IsTrue(AssetDatabase.Contains(prefab));
            Assert.That(EntityWizardStep.For("Assets"), Is.Empty);
        }

        [Test]
        public void EveryTemplateIsOffered()
        {
            var templates = EntityChoice.All().Single(group => group.Heading.Label == "Templates").Choices;

            Assert.AreEqual(TemplateRegistry.All.Count, templates.Count);
            Assert.That(templates.Select(choice => choice.IsHeading), Has.All.False);
        }

        /// <summary>Applies only to the test folder and keeps the prefab it was given.</summary>
        public sealed class RecordingStep : EntityWizardStep
        {
            public GameObject Seen;

            public override bool Applies(string folder) => folder == Folder;

            public override VisualElement CreateForm(Action changed) => new();

            public override string Problem(string name) => null;

            public override void Run(GameObject prefab) => Seen = prefab;
        }
    }
}
