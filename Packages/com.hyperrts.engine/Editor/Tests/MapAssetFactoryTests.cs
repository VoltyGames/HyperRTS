using System;
using System.IO;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Factories;
using HyperRTS.Editor.Maps.Library;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>
    /// Library items that make their own assets: the factory's params block read and written back untouched, params
    /// that miss or misname a key, a material whose texture set is incomplete, and import batches.
    /// </summary>
    public class MapAssetFactoryTests
    {
        private const string LayerJson =
            "{\n" +
            "  \"terrainLayer\": \"Assets/Never/TL_Meadow.terrainlayer\",\n" +
            "  \"mapColor\": {\n    \"r\": 0.5,\n    \"g\": 0.5,\n    \"b\": 0.5,\n    \"a\": 1.0\n  },\n" +
            "  \"rotation\": 0.0,\n  \"followsRows\": false,\n  \"heightBlend\": 1.0,\n" +
            "  \"macroStrength\": 1.0,\n  \"antiTiling\": 1.0,\n" +
            "  \"factory\": \"terrainLayer\",\n" +
            "  \"params\": {\n    \"textureSet\": \"TerrainMeadow\",\n    \"textureSize\": 1024,\n    \"tileSize\": 4.5\n  }\n" +
            "}\n";

        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "MapAssetFactoryTests-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void ParamsBlock_RoundTripsThroughTheItemFile()
        {
            var file = Write("Meadow/Meadow.layer.json", LayerJson);

            var read = MapLibraryItemFile.Read(typeof(MapClimateLayer), file);

            Assert.AreEqual("terrainLayer", read.Refs.factory);
            Assert.AreEqual(LayerJson, MapLibraryItemFile.ToJson(typeof(MapClimateLayer), read.Item, read.Refs));
        }

        [Test]
        public void ParamsBlock_ReadsIntoTheFactorysClass()
        {
            var file = Write("Meadow/Meadow.layer.json", LayerJson);

            var settings = Request(typeof(MapClimateLayer), "Meadow", file).Params<TerrainLayerParams>();

            Assert.AreEqual("TerrainMeadow", settings.textureSet);
            Assert.AreEqual(1024, settings.textureSize);
            Assert.AreEqual(4.5f, settings.tileSize);
        }

        [Test]
        public void Params_MissingOrUnknownKeys_FailNamingThem()
        {
            var json = LayerJson.Replace("\"tileSize\"", "\"tileSzie\"");
            var file = Write("Meadow/Meadow.layer.json", json);

            var error = Assert.Throws<InvalidOperationException>(() =>
                Request(typeof(MapClimateLayer), "Meadow", file).Params<TerrainLayerParams>());

            StringAssert.Contains("tileSize", error.Message);
            StringAssert.Contains("tileSzie", error.Message);
            StringAssert.Contains(file, error.Message);
        }

        [Test]
        public void MaterialItem_WithAMissingTexture_FailsNamingTheFile()
        {
            Write("Brick/Brick.material.json",
                "{\n  \"material\": \"Assets/Never/M_Brick.mat\",\n  \"factory\": \"photoMaterial\",\n" +
                "  \"params\": {\n    \"textureSet\": \"Brick\",\n    \"textureSize\": 1024,\n    \"smoothness\": 1.0\n  }\n}\n");

            var error = Assert.Throws<FileNotFoundException>(() =>
                MapLibraryAssets.Generate(MapLibrary.Load(_root), typeof(MapMaterial), "Brick"));

            StringAssert.Contains($"{_root}/Brick/T_Brick_BaseColor.png", error.Message);
        }

        [Test]
        public void Factory_ThatNoClassHas_FailsNamingTheFileAndKind()
        {
            var file = Write("Glass.material.json",
                "{\n  \"material\": \"Assets/Never/M_Glass.mat\",\n  \"factory\": \"nope\",\n  \"params\": {}\n}\n");

            var error = Assert.Throws<InvalidOperationException>(() =>
                MapLibraryAssets.Generate(MapLibrary.Load(_root), typeof(MapMaterial), "Glass"));

            StringAssert.Contains(file, error.Message);
            StringAssert.Contains("'nope'", error.Message);
        }

        [Test]
        public void Queue_SchedulesOneFlushPerBatch()
        {
            var queue = new MapAssetQueue();

            Assert.IsTrue(queue.Add("Assets/A.layer.json"));
            Assert.IsFalse(queue.Add("Assets/B.prop.json"));
            Assert.IsFalse(queue.Add("Assets/A.layer.json"));
            CollectionAssert.AreEqual(new[] { "Assets/A.layer.json", "Assets/B.prop.json" }, queue.Take());
            Assert.AreEqual(0, queue.Count);
            Assert.IsTrue(queue.Add("Assets/C.tree.json"));
        }

        [Test]
        public void LibraryFiles_AreToldFromOtherJson()
        {
            Assert.AreEqual((typeof(MapMaterial), "Brick"), MapLibrary.ParseFile("Assets/X/Brick.material.json"));
            Assert.IsNull(MapLibrary.ParseFile("Assets/X/Brick.json").Type);
            Assert.IsNull(MapLibrary.ParseFile("Assets/X/Brick.unknown.json").Type);
            Assert.IsNull(MapLibrary.ParseFile("Assets/X/Brick.material.json.meta").Type);
        }

        [Test]
        public void JsonMembers_KeepNestedValuesAsWritten()
        {
            var members = MapJsonMembers.Of("{ \"a\": \"x,}\", \"b\": { \"c\": [1, {\"d\": 2}] }, \"e\": 3.5 }");

            Assert.AreEqual(3, members.Count);
            Assert.AreEqual("\"x,}\"", members[0].Value);
            Assert.AreEqual("{ \"c\": [1, {\"d\": 2}] }", members[1].Value);
            Assert.AreEqual("3.5", members[2].Value);
        }

        private MapAssetRequest Request(Type type, string key, string file) =>
            new(MapLibrary.Load(_root), type, key, file);

        private string Write(string relative, string json)
        {
            var path = $"{_root}/{relative}";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            return path;
        }
    }
}
