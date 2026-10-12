using System;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>
    /// Editing the map library: what names an item, new items written and read back unchanged, the file format,
    /// file names and the load problems the Library page lists.
    /// </summary>
    public class MapLibraryEditingTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "MapLibraryEditingTests-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Write("Layers/Meadow.layer.json", "{}");
            Write("Library/Meadow.surface.json", "{\"layer\": \"Meadow\"}");
            Write("Library/Lane.road.json", "{\"surface\": \"Meadow\", \"vergeSurface\": \"Meadow\"}");
            Write("Library/Steppe.climate.json", "{\"surfaces\": [\"Meadow\"], \"roadClasses\": [\"Lane\"], \"groundSurface\": \"Meadow\"}");
            Write("Library/Village.settlement.json", "{\"yardSurface\": \"Field\", \"houses\": [\"House\"]}");
            Write("Maps/Test.map.json", "{}");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void UsedBy_FindsItemsWhoseKeyFieldsNameTheItem()
        {
            var usages = MapLibraryUsages.Find(MapLibrary.Load(_root), typeof(MapClimateSurface), "Meadow");

            CollectionAssert.AreEquivalent(
                new[] { "Lane.surface", "Lane.vergeSurface", "Steppe.surfaces", "Steppe.groundSurface" },
                usages.Select(usage => $"{usage.Key}.{usage.Field}"));
            Assert.IsTrue(usages.All(usage => usage.File.StartsWith(_root, StringComparison.Ordinal)));
        }

        [Test]
        public void UsedBy_MatchesOnlyFieldsOfTheItemsType()
        {
            var usages = MapLibraryUsages.Find(MapLibrary.Load(_root), typeof(MapClimateLayer), "Meadow");

            Assert.AreEqual(1, usages.Count);
            Assert.AreEqual(typeof(MapClimateSurface), usages[0].Type);
            Assert.AreEqual("layer", usages[0].Field);
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
        public void Files_PutTheTypesRefsFirstIndentedByTwoSpaces()
        {
            var layer = new MapClimateLayer { rotation = 90f };
            var refs = new MapLibraryRefs { terrainLayer = "Assets/Meadow.terrainlayer", prefab = "Assets/Unused.prefab" };

            var json = MapLibraryItemFile.ToJson(typeof(MapClimateLayer), layer, refs);

            StringAssert.StartsWith("{\n  \"terrainLayer\": \"Assets/Meadow.terrainlayer\",\n  \"mapColor\": {\n    \"r\":", json);
            StringAssert.Contains("\n  \"rotation\": 90,\n", json);
            StringAssert.EndsWith("\n}\n", json);
            Assert.IsFalse(json.Contains("prefab"));
        }

        [Test]
        public void FileNames_MustBeKeyDotType()
        {
            Assert.AreEqual("Meadow", LibraryFiles.ParseKey(typeof(MapClimateSurface), "Assets/X/Meadow.surface.json"));
            Assert.Throws<ArgumentException>(() => LibraryFiles.ParseKey(typeof(MapClimateSurface), "Assets/Meadow.json"));
            Assert.Throws<ArgumentException>(() => LibraryFiles.ParseKey(typeof(MapClimateSurface), "Assets/A.B.surface.json"));
        }

        [Test]
        public void Problems_NameTheFileAndTheMissingKey()
        {
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

        private void Write(string path, string json)
        {
            var file = Path.Combine(_root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, json);
        }
    }
}
