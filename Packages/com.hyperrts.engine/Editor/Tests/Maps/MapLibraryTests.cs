using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps
{
    /// <summary>
    /// The map library: items keyed by file name, climates pulling in what they use, item files written and read
    /// back, asset links, and every bad file an error naming it.
    /// </summary>
    public class MapLibraryTests
    {
        private const string AssetFolder = "Assets/MapLibraryTest";
        private const string AssetPath = AssetFolder + "/Ground.mat";

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "MapLibraryTests-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Write("Layers/Meadow.layer.json", "{}");
            Write("Layers/Stubble.layer.json", "{}");
            Write("Plants/Grass.plant.json", "{}");
            Write("Trees/Oak.tree.json", "{\"scale\": {\"x\": 0.9, \"y\": 1.1}}");
            Write("Library/Meadow.surface.json",
                "{\"layer\": \"Meadow\", \"trees\": [{\"tree\": \"Oak\", \"perHectare\": 10}], \"plants\": [{\"plant\": \"Grass\", \"coverage\": 0.5}]}");
            Write("Library/Stubble.surface.json", "{\"layer\": \"Stubble\"}");
            Write("Library/Lane.road.json", "{\"surface\": \"Meadow\", \"vergeSurface\": \"Meadow\", \"width\": 5}");
            Write("Library/Steppe.climate.json",
                "{\"surfaces\": [\"Meadow\", \"Stubble\"], \"roadClasses\": [\"Lane\"], \"groundSurface\": \"Meadow\"}");
            Write("Library/Shed.prop.json", "{\"size\": {\"x\": 3, \"y\": 2}}");
            Write("Library/Game.palette.json", "{\"baseRadius\": 12}");
            Write("Library/Game.rules.json", "{\"vehicleSpeed\": 7}");
            Write("Library/Notes.json", "{}");
            Write("Maps/Test.map.json", "{}");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_root, true);
            AssetDatabase.DeleteAsset(AssetFolder);
        }

        [Test]
        public void AClimate_TakesItsKeysFromFileNamesAndPullsInWhatItUses()
        {
            var climate = MapLibrary.Load(_root).Climate("Steppe", "test");

            Assert.AreEqual("Steppe", climate.name);
            Assert.AreEqual("Meadow", climate.groundSurface);
            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, climate.surfaces.Select(s => s.key));
            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, climate.layers.Select(l => l.key));
            CollectionAssert.AreEqual(new[] { "Oak" }, climate.trees.Select(t => t.key));
            CollectionAssert.AreEqual(new[] { "Grass" }, climate.plants.Select(d => d.key));
            Assert.AreEqual(5f, climate.RoadClass("Lane").width);
            Assert.AreEqual(1.1f, climate.trees[0].scale.y, 1e-5f);
        }

        [Test]
        public void AClimatesWarKit_PullsInItsDecalsPlantLayerAndShreddedTrees()
        {
            Write("Trees/OakSnapped.tree.json", "{}");
            Write("Decals/Crater.decal.json", "{}");
            Write("Library/Old.war.json",
                "{\"craterDecal\": \"Crater\", \"wetDecal\": \"Crater\", \"weedPlant\": \"Grass\", \"burntLayer\": \"Stubble\", " +
                "\"shreddedTrees\": [{\"tree\": \"Oak\", \"shredded\": [\"OakSnapped\"]}]}");
            Write("Library/Scarred.climate.json",
                "{\"surfaces\": [\"Meadow\"], \"roadClasses\": [\"Lane\"], \"groundSurface\": \"Meadow\", \"warKit\": \"Old\"}");

            var climate = MapLibrary.Load(_root).Climate("Scarred", "test");

            Assert.AreEqual("Old", climate.war.key);
            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, climate.layers.Select(l => l.key));
            CollectionAssert.AreEqual(new[] { "Oak", "OakSnapped" }, climate.trees.Select(t => t.key));
            CollectionAssert.AreEqual(new[] { "Crater" }, climate.decals.Select(d => d.key));
            Assert.IsNull(MapLibrary.Load(_root).Climate("Steppe", "test").war);
        }

        [Test]
        public void ThePalette_HoldsEveryProp_AndTheRulesLoad()
        {
            var library = MapLibrary.Load(_root);
            var palette = library.Palette();

            Assert.AreEqual(12f, palette.baseRadius);
            CollectionAssert.AreEqual(new[] { "Shed" }, palette.entries.Select(e => e.key));
            Assert.AreEqual(7f, library.Rules().vehicleSpeed);
        }

        [Test]
        public void PaletteSides_ReadTheirKitsAndBuildOrders()
        {
            Write("Library/Game.palette.json",
                "{\"sides\": [{\"side\": 3, \"startKit\": [{\"key\": \"Shed\", \"offset\": {\"x\": 2, \"y\": 5}, \"yaw\": 90}], \"buildOrder\": {}}]}");

            var side = MapLibrary.Load(_root).Palette().sides.Single();

            Assert.AreEqual(3, side.side);
            Assert.AreEqual("Shed", side.startKit.Single().key);
            Assert.AreEqual(new Vector2(2f, 5f), side.startKit.Single().offset);
            Assert.AreEqual(90f, side.startKit.Single().yaw);
            Assert.IsNull(side.buildOrderAsset, "an empty build order lets the side's bots train freely");
        }

        [Test]
        public void AFileWithoutTheVersion_ThrowsNamingIt()
        {
            File.WriteAllText(Path.Combine(_root, "Library/Game.palette.json"), "{\"baseRadius\": 12}");

            var error = Assert.Throws<InvalidOperationException>(() => MapLibrary.Load(_root).Palette());

            StringAssert.Contains("has version none, but the library reads version 1", error.Message);
        }

        [Test]
        public void AnAssetLinkWrittenAsAPath_ThrowsNamingTheField()
        {
            Write("Library/Game.palette.json", "{\"rig\": \"Assets/Rig.prefab\"}");

            var error = Assert.Throws<InvalidOperationException>(() => MapLibrary.Load(_root).Palette());

            StringAssert.Contains("'rig' must be an object", error.Message);
        }

        [Test]
        public void ANestedFieldTheItemDoesNotRead_ThrowsNamingItsPath()
        {
            Write("Library/Game.palette.json", "{\"sides\": [{\"sied\": 3}]}");

            var error = Assert.Throws<InvalidOperationException>(() => MapLibrary.Load(_root).Palette());

            StringAssert.Contains("'sides[0].sied' is not a palette field", error.Message);
            StringAssert.Contains("Game.palette.json", error.Message);
        }

        [Test]
        public void ADuplicateKey_ThrowsNamingBothFiles()
        {
            Write("Elsewhere/Meadow.layer.json", "{}");

            var error = Assert.Throws<InvalidOperationException>(() => MapLibrary.Load(_root));

            StringAssert.Contains($"{_root}/Elsewhere/Meadow.layer.json", error.Message);
            StringAssert.Contains($"{_root}/Layers/Meadow.layer.json", error.Message);
        }

        [Test]
        public void AMissingKey_ThrowsNamingTheFileThatNamesIt()
        {
            Write("Library/Stubble.surface.json", "{\"layer\": \"Stubble\", \"trees\": [{\"tree\": \"Pine\"}]}");

            var error = Assert.Throws<KeyNotFoundException>(() => MapLibrary.Load(_root).Climate("Steppe", "test"));

            StringAssert.Contains("Pine.tree.json", error.Message);
            StringAssert.Contains($"{_root}/Library/Stubble.surface.json", error.Message);
        }

        [Test]
        public void AMissingClimate_ThrowsNamingTheSpec()
        {
            var error = Assert.Throws<KeyNotFoundException>(() => MapLibrary.Load(_root).Climate("Tundra", "Maps/Test.json"));

            StringAssert.Contains("Maps/Test.json", error.Message);
            StringAssert.Contains("Tundra.climate.json", error.Message);
        }

        [Test]
        public void TwoPalettes_Throw()
        {
            Write("Library/Other.palette.json", "{}");

            Assert.Throws<InvalidOperationException>(() => MapLibrary.Load(_root).Palette());
        }

        [Test]
        public void NewItems_RoundTripThroughTheirFiles()
        {
            Directory.CreateDirectory($"{_root}/New");
            foreach (var type in MapLibrary.ItemTypes)
            {
                var file = $"{_root}/New/New.{MapLibrary.TypeName(type)}.json";
                var (item, refs) = MapLibraryItemFile.Create(type);
                var json = MapLibraryItemFile.ToJson(type, item, refs);
                MapLibraryItemFile.Write(file, type, item, refs);

                var read = MapLibraryItemFile.Read(type, file);
                Assert.AreEqual(json, MapLibraryItemFile.ToJson(type, read.Item, read.Refs), type.Name);
                Assert.IsFalse(json.Contains("\"key\""), $"{type.Name} writes its key.");
                CollectionAssert.Contains(MapLibrary.Load(_root).Keys(type), "New", type.Name);
            }
        }

        [Test]
        public void Files_PutTheVersionAndTheTypesRefsFirstIndentedByTwoSpaces()
        {
            var layer = new MapClimateLayer { rotation = 90f };
            var refs = new MapLibraryRefs
            {
                terrainLayer = new MapAssetRef { path = "Assets/Meadow.terrainlayer", guid = "0123" },
                prefab = new MapAssetRef { path = "Assets/Unused.prefab" },
            };

            var json = MapLibraryItemFile.ToJson(typeof(MapClimateLayer), layer, refs);

            const string head = "{\n  \"version\": 1,\n  \"terrainLayer\": {\n    \"path\": \"Assets/Meadow.terrainlayer\",\n"
                + "    \"guid\": \"0123\"\n  },\n  \"mapColor\": {\n    \"r\":";
            StringAssert.StartsWith(head, json);
            StringAssert.Contains("\n  \"rotation\": 90,\n", json);
            StringAssert.EndsWith("\n}\n", json);
            Assert.IsFalse(json.Contains("prefab"));
        }

        [Test]
        public void Problems_NameTheFileAndTheMissingKey()
        {
            Write("Library/Village.settlement.json", "{\"yardSurface\": \"Field\", \"houses\": [\"House\"]}");

            var problems = MapLibraryProblems.Find(_root);

            Assert.AreEqual(2, problems.Count, string.Join("\n", problems));
            Assert.IsTrue(problems.All(problem => problem.File.EndsWith("Village.settlement.json", StringComparison.Ordinal)));
            Assert.IsTrue(problems.Any(problem => problem.Message.Contains("yardSurface") && problem.Message.Contains("'Field'")));
            Assert.IsTrue(problems.Any(problem => problem.Message.Contains("houses") && problem.Message.Contains("'House'")));
        }

        [Test]
        public void Problems_ReportAFileThatDoesNotParse()
        {
            Write("Library/Broken.surface.json", "{\"layer\": ");

            var problems = MapLibraryProblems.Find(_root);

            Assert.IsTrue(problems.Any(problem => problem.File.EndsWith("Broken.surface.json", StringComparison.Ordinal)));
        }

        [Test]
        public void AnEditedAssetPath_FillsInItsGuid()
        {
            CreateAsset();
            var link = new MapAssetRef { path = AssetPath, guid = "stale" };

            Assert.IsTrue(link.Sync());
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(AssetPath), link.guid);
        }

        [Test]
        public void AMovedAsset_StillLoads_AndSyncWritesItsNewPath()
        {
            const string moved = AssetFolder + "/Moved.mat";
            CreateAsset();
            var link = MapAssetRef.At(AssetPath);
            AssetDatabase.MoveAsset(AssetPath, moved);

            Assert.IsNotNull(link.Load(typeof(Material), out _));
            Assert.IsTrue(link.Sync());
            Assert.AreEqual(moved, link.path);
            Assert.IsFalse(link.Sync(), "a synced link is stable");
        }

        [Test]
        public void AnAssetLinkWithoutAGuid_DoesNotLoad_AndSaysWhy()
        {
            CreateAsset();
            var link = new MapAssetRef { path = AssetPath };

            Assert.IsNull(link.Load(typeof(Material), out var problem));
            StringAssert.Contains("has no guid", problem);
        }

        private static void CreateAsset()
        {
            AssetDatabase.CreateFolder("Assets", "MapLibraryTest");
            AssetDatabase.CreateAsset(new Material(Shader.Find("Hidden/InternalErrorShader")), AssetPath);
        }

        private void Write(string path, string json)
        {
            var file = Path.Combine(_root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, MapLibrary.ParseFile(path).Type == null ? json : Versioned(json));
        }

        /// <summary>Library fixtures carry the version real files do.</summary>
        private static string Versioned(string json)
        {
            var rest = json[1..].TrimStart();
            var separator = rest.StartsWith('}') ? string.Empty : ", ";
            return $"{{\"version\": {MapLibraryItemFile.Version}{separator}{rest}";
        }
    }
}
