using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Simulation.GameEntities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        public void EveryTemplateIsOffered()
        {
            var templates = EntityChoice.All().Single(group => group.Heading.Label == "Templates").Choices;

            Assert.AreEqual(TemplateRegistry.All.Count, templates.Count);
            Assert.That(templates.Select(choice => choice.IsHeading), Has.All.False);
        }
    }
}
