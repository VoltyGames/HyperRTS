using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>
    /// The map library: items keyed by their file names, a climate's layers, trees and plants pulled in from what
    /// it uses, and a missing, duplicate or ambiguous file is an error naming the file.
    /// </summary>
    public class MapLibraryTests
    {
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
            Write("Library/Lane.road.json", "{\"surface\": \"Meadow\", \"width\": 5}");
            Write("Library/Steppe.climate.json",
                "{\"surfaces\": [\"Meadow\", \"Stubble\"], \"roadClasses\": [\"Lane\"], \"groundSurface\": \"Meadow\"}");
            Write("Library/Shed.prop.json", "{\"size\": {\"x\": 3, \"y\": 2}}");
            Write("Library/Game.palette.json", "{\"baseRadius\": 12}");
            Write("Library/Game.rules.json", "{\"vehicleSpeed\": 7}");
            Write("Library/Notes.json", "{}");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

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
        public void KeysAreUniquePerType()
        {
            var library = MapLibrary.Load(_root);

            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, library.Keys<MapClimateLayer>());
            CollectionAssert.AreEqual(new[] { "Meadow", "Stubble" }, library.Keys<MapClimateSurface>());
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
            Assert.AreEqual(new UnityEngine.Vector2(2f, 5f), side.startKit.Single().offset);
            Assert.AreEqual(90f, side.startKit.Single().yaw);
            Assert.IsNull(side.buildOrderAsset, "an empty build order lets the side's bots train freely");
        }

        [Test]
        public void AFieldTheItemDoesNotRead_Throws_LikeTheOldPaletteStartKit()
        {
            Write("Library/Game.palette.json", "{\"startKit\": [{\"key\": \"Shed\"}]}");

            var error = Assert.Throws<InvalidOperationException>(() => MapLibrary.Load(_root).Palette());

            StringAssert.Contains("'startKit' is not a palette field", error.Message);
            StringAssert.Contains("Game.palette.json", error.Message);
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
