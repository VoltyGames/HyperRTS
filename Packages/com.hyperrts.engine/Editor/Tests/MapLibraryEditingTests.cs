using System;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>
    /// The map library files: new items written and read back unchanged, the file format and the load problems
    /// <see cref="MapLibraryProblems"/> reports.
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
