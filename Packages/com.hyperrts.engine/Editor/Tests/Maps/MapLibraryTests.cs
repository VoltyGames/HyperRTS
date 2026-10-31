using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Rules;
using HyperRTS.Editor.Validation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>
    /// The map library on items made in memory: items keyed by name, climates pulling in what they use, and every
    /// bad item an error naming it.
    /// </summary>
    public class MapLibraryTests
    {
        private readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            _made.ForEach(Object.DestroyImmediate);
            _made.Clear();
        }

        [Test]
        public void AClimate_PullsInWhatItUses()
        {
            var climate = SteppeLibrary().Climate("Steppe", "test");

            Assert.AreEqual("Steppe", climate.key);
            Assert.AreEqual("Meadow", climate.groundSurface);
            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, climate.surfaces.Select(s => s.key));
            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, climate.layers.Select(l => l.key));
            CollectionAssert.AreEqual(new[] { "Oak" }, climate.trees.Select(t => t.key));
            CollectionAssert.AreEqual(new[] { "Grass" }, climate.plants.Select(d => d.key));
            Assert.AreEqual(5f, climate.RoadClass("Lane").width);
            Assert.AreEqual(1.1f, climate.trees[0].scale.y, 1e-5f);
        }

        [Test]
        public void AnItem_LoadsAsACopy_AndOriginalIsTheItem()
        {
            var layer = Make<MapClimateLayer>("Meadow");
            var library = MapLibrary.From(layer);

            var copy = library.Get<MapClimateLayer>("Meadow", "test");

            Assert.AreNotSame(layer, copy);
            Assert.AreSame(copy, library.Get<MapClimateLayer>("Meadow", "test"), "a library loads each item once");
            Assert.AreSame(layer, library.Original(typeof(MapClimateLayer), "Meadow"));
            Assert.AreEqual("Meadow", copy.key);
        }

        [Test]
        public void AClimatesWarKit_PullsInItsDecalsPlantLayerAndShreddedTrees()
        {
            var war = Make<MapWarKit>("Old", w =>
            {
                w.craterDecal = "Crater";
                w.wetDecal = "Crater";
                w.weedPlant = "Grass";
                w.burntLayer = "Stubble";
                w.shreddedTrees.Add(new MapWarTreeSwap { tree = "Oak", shredded = new List<string> { "OakSnapped" } });
            });
            var scarred = Make<MapClimate>("Scarred", c =>
            {
                c.surfaceKeys.Add("Meadow");
                c.roadClassKeys.Add("Lane");
                c.groundSurface = "Meadow";
                c.warKit = "Old";
            });
            var library = SteppeLibrary(war, scarred, Make<MapClimateTree>("OakSnapped"), Make<MapClimateDecal>("Crater"));

            var climate = library.Climate("Scarred", "test");

            Assert.AreEqual("Old", climate.war.key);
            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, climate.layers.Select(l => l.key));
            CollectionAssert.AreEqual(new[] { "Oak", "OakSnapped" }, climate.trees.Select(t => t.key));
            CollectionAssert.AreEqual(new[] { "Crater" }, climate.decals.Select(d => d.key));
            Assert.IsNull(library.Climate("Steppe", "test").war);
        }

        [Test]
        public void ThePalette_HoldsEveryProp_AndTheRulesLoad()
        {
            var library = MapLibrary.From(
                Make<MapPalette>("Game", p => p.baseRadius = 12f),
                Make<MapRules>("Game", r => r.vehicleSpeed = 7f),
                Make<MapProp>("Shed", p => p.size = new Vector2(3f, 2f)));

            var palette = library.Palette();

            Assert.AreEqual(12f, palette.baseRadius);
            CollectionAssert.AreEqual(new[] { "Shed" }, palette.entries.Select(e => e.key));
            Assert.AreEqual(7f, library.Rules().vehicleSpeed);
        }

        [Test]
        public void ADuplicateKey_ThrowsNamingTheKey()
        {
            var error = Assert.Throws<InvalidOperationException>(
                () => MapLibrary.From(Make<MapClimateLayer>("Meadow"), Make<MapClimateLayer>("Meadow")));

            StringAssert.Contains("both layer 'Meadow'", error.Message);
        }

        [Test]
        public void AMissingKey_ThrowsNamingTheItemThatNamesIt()
        {
            var library = SteppeLibrary(Make<MapClimateSurface>("Stubble", s =>
            {
                s.layer = "Stubble";
                s.trees.Add(new MapTreeDensity { tree = "Pine" });
            }));

            var error = Assert.Throws<KeyNotFoundException>(() => library.Climate("Steppe", "test"));

            StringAssert.Contains("Pine.tree.asset", error.Message);
            StringAssert.Contains("'Stubble' names it", error.Message);
        }

        [Test]
        public void AMissingClimate_ThrowsNamingTheSpec()
        {
            var error = Assert.Throws<KeyNotFoundException>(() => SteppeLibrary().Climate("Tundra", "Maps/Test.json"));

            StringAssert.Contains("Maps/Test.json", error.Message);
            StringAssert.Contains("Tundra.climate.asset", error.Message);
        }

        [Test]
        public void TwoPalettes_Throw()
        {
            var library = MapLibrary.From(Make<MapPalette>("Game"), Make<MapPalette>("Other"));

            Assert.Throws<InvalidOperationException>(() => library.Palette());
        }

        [Test]
        public void TheRule_NamesAKeyThatNamesNoItem()
        {
            var library = MapLibrary.From(Make<MapClimateSurface>("Village", s => s.layer = "Field"));
            var issues = new ValidationIssues();

            MapLibraryRule.Check(library, issues);

            Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
            StringAssert.Contains("layer names", issues[0].Message);
            StringAssert.Contains("'Field'", issues[0].Message);
        }

        [Test]
        public void TheRule_FindsATreeWithMismatchedDestructibles()
        {
            var tree = Make<MapClimateTree>("Oak", t =>
            {
                t.prefabs.AddRange(new[] { new GameObject("A"), new GameObject("B") });
                t.destructibles.Add(new GameObject("C"));
            });
            _made.AddRange(tree.prefabs);
            _made.AddRange(tree.destructibles);
            var issues = new ValidationIssues();

            MapLibraryRule.Check(MapLibrary.From(tree), issues);

            Assert.AreEqual(1, issues.Count, string.Join("\n", issues));
            StringAssert.Contains("1 destructibles for 2 prefabs", issues[0].Message);
        }

        /// <summary>A small landscape (layers, tree, plant, road, surfaces and the climate Steppe), with <paramref name="extra"/> added or replacing an item of the same type and key.</summary>
        private MapLibrary SteppeLibrary(params MapLibraryItem[] extra)
        {
            var items = new List<MapLibraryItem>
            {
                Make<MapClimateLayer>("Meadow"),
                Make<MapClimateLayer>("Stubble"),
                Make<MapClimatePlant>("Grass"),
                Make<MapClimateTree>("Oak", t => t.scale = new Vector2(0.9f, 1.1f)),
                Make<MapClimateSurface>("Meadow", s =>
                {
                    s.layer = "Meadow";
                    s.trees.Add(new MapTreeDensity { tree = "Oak", perHectare = 10f });
                    s.plants.Add(new MapPlantCoverage { plant = "Grass", coverage = 0.5f });
                }),
                Make<MapClimateSurface>("Stubble", s => s.layer = "Stubble"),
                Make<MapRoadClass>("Lane", r =>
                {
                    r.surface = "Meadow";
                    r.vergeSurface = "Meadow";
                    r.width = 5f;
                }),
                Make<MapClimate>("Steppe", c =>
                {
                    c.surfaceKeys.AddRange(new[] { "Meadow", "Stubble" });
                    c.roadClassKeys.Add("Lane");
                    c.groundSurface = "Meadow";
                }),
            };
            foreach (var item in extra)
            {
                var replaced = items.FindIndex(own => own.GetType() == item.GetType() && own.key == item.key);
                if (replaced >= 0)
                {
                    items[replaced] = item;
                }
                else
                {
                    items.Add(item);
                }
            }

            return MapLibrary.From(items.ToArray());
        }

        private T Make<T>(string key, Action<T> init = null) where T : MapLibraryItem
        {
            var item = MapFixture.Item<T>(key);
            init?.Invoke(item);
            _made.Add(item);
            return item;
        }
    }
}
