using System.Linq;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Transport;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Validation
{
    /// <summary>Each validation quick fix repairs what its issue reports.</summary>
    public class QuickFixTests : TemplateFixture
    {
        private const string Folder = "Assets/_QuickFixTests";

        [TearDown]
        public void DeleteFolder() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void FitPickBoxWrapsTheModel()
        {
            var unit = Track(EntityTemplates.Unit());
            Object.DestroyImmediate(unit.GetComponent<PickBoxAuthoring>());

            Fix(unit.GetComponent<UnitAuthoring>(), "Fit Pick Box");

            var box = unit.GetComponent<PickBoxAuthoring>();
            Assert.IsNotNull(box);
            Assert.AreEqual(1.8f, box.size.y, 0.01f);
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
        public void AddFlightTurnsAnAirLayerUnitIntoAnAircraft()
        {
            var unit = Track(EntityTemplates.Unit()).GetComponent<UnitAuthoring>();
            unit.navLayer = NavLayer.Air;

            Fix(unit, "Add Flight");

            Assert.IsTrue(unit.TryGetComponent<FlightAuthoring>(out _));
            Assert.IsFalse(ProjectValidator.Check(unit).Any(issue => issue.FixLabel == "Add Flight"));
        }

        [Test]
        public void AddRequiredPutsTheMissingAuthoringOnTheObject()
        {
            var producer = Track(new GameObject("Producer")).AddComponent<ProducerAuthoring>();

            Fix(producer, "Add Building");

            Assert.IsTrue(producer.TryGetComponent<BuildingAuthoring>(out _));
        }

        [Test]
        public void RemoveEmptyDropsArmorEntriesWithoutADamageType()
        {
            var armor = Track(EntityTemplates.Unit()).AddComponent<ArmorAuthoring>();
            armor.modifiers.Add(new ArmorAuthoring.Entry { multiplier = 2f });

            Fix(armor, "Remove Empty");

            Assert.IsEmpty(armor.modifiers);
        }

        [Test]
        public void ClampOversizedPassengerLimitToCapacity()
        {
            var container = Track(EntityTemplates.Building()).AddComponent<ContainerAuthoring>();
            container.capacity = 2;
            container.maxPassengerSize = 4;

            ProjectValidator.Check(container).Single(found => found.Fix != null).Fix();

            Assert.AreEqual(2, container.maxPassengerSize);
        }

        private static void Fix(Component component, string label) =>
            ProjectValidator.Check(component).First(issue => issue.FixLabel == label).Fix();
    }
}
