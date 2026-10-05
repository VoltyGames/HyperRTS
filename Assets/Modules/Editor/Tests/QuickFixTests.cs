using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class QuickFixTests
    {
        private const string Folder = "Assets/_QuickFixTests";
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            _created.ForEach(Object.DestroyImmediate);
            _created.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void FitColliderWrapsTheModel()
        {
            var unit = Track(EntityTemplates.Unit());
            Object.DestroyImmediate(unit.GetComponent<Collider>());

            Fix(unit.GetComponent<UnitAuthoring>(), "Fit Collider");

            var collider = unit.GetComponent<BoxCollider>();
            Assert.IsNotNull(collider);
            Assert.AreEqual(1.8f, collider.size.y, 0.01f);
        }

        [Test]
        public void MoveOutsidePutsTheSpawnPointBeyondTheFootprint()
        {
            var producer = Track(EntityTemplates.Producer()).GetComponent<ProducerAuthoring>();
            producer.spawnOffset = Vector3.zero;

            Fix(producer, "Move Outside");

            Assert.AreEqual(-3.5f, producer.spawnOffset.z, 0.01f);
        }

        [Test]
        public void UsePrefabSwapsTheSceneInstanceForItsAsset()
        {
            AssetDatabase.CreateFolder("Assets", "_QuickFixTests");
            var asset = PrefabUtility.SaveAsPrefabAsset(Track(EntityTemplates.Unit()), Folder + "/Unit.prefab");
            var instance = Track((GameObject)PrefabUtility.InstantiatePrefab(asset)).GetComponent<UnitAuthoring>();
            var producer = Track(EntityTemplates.Producer()).GetComponent<ProducerAuthoring>();
            producer.productionOptions.Add(instance);
            producer.productionOptions.Add(null);

            Fix(producer, "Use Prefab");
            Fix(producer, "Remove Empty");

            CollectionAssert.AreEqual(new[] { asset.GetComponent<UnitAuthoring>() }, producer.productionOptions);
        }

        [Test]
        public void SummaryListsRoleModulesDamageAndCost()
        {
            var soldier = Track(EntityTemplates.CombatUnit()).GetComponent<GameEntityAuthoring>();

            Assert.AreEqual("Unit · Weapon · 10 DPS · free", EntitySummary.Line(soldier));
        }

        private static void Fix(Component component, string label) =>
            AuthoringChecks.For(component).First(issue => issue.FixLabel == label).Fix();

        private GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }
    }
}
