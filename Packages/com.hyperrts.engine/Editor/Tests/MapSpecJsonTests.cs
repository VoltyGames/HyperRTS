using System.Collections.Generic;
using System.IO;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Json;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;

namespace HyperRTS.Editor.Tests
{
    /// <summary>The Map Editor's spec writer: compact layout, lossless round trips and stable output.</summary>
    public class MapSpecJsonTests
    {
        [Test]
        public void PointsAndShortArraysSitOnOneLine()
        {
            var json = MapSpecJson.Write(Sample());

            StringAssert.Contains("\"size\": { \"x\": 400, \"z\": 300 }", json);
            StringAssert.Contains("\"pos\": { \"x\": -55, \"z\": -165.5 }", json);
            StringAssert.Contains("\"notes\": [\"first\"]", json);
            StringAssert.Contains("\"crossings\": []", json);
            StringAssert.StartsWith("{\n  \"name\": \"Sample\",", json);
        }

        [Test]
        public void SampleRoundTrips() => AssertRoundTrips(MapSpecJson.Write(Sample()), "sample");

        /// <summary>Every spec in the project survives load, write, load unchanged, and writing twice is stable.</summary>
        [Test]
        public void ProjectSpecsRoundTrip()
        {
            foreach (var path in MapPaths.All())
            {
                AssertRoundTrips(File.ReadAllText(path), path);
            }
        }

        [Test]
        public void FormatIsStable()
        {
            var once = MapJsonFormat.Format("{\"a\":[1.0,2.5],\"b\":{\"x\":1,\"z\":2},\"c\":[{\"d\":\"e\\\"f\"}]}");

            Assert.AreEqual(once, MapJsonFormat.Format(once));
            StringAssert.Contains("\"a\": [1, 2.5]", once);
            StringAssert.Contains("\"d\": \"e\\\"f\"", once);
        }

        [Test]
        public void MalformedJsonFailsLoudly()
        {
            Assert.Throws<System.FormatException>(() => MapJsonFormat.Format("{\"a\": [1, 2}"));
        }

        [Test]
        public void ANestedFieldTheSpecDoesNotRead_ThrowsNamingItsPath()
        {
            const string json = "{\"name\": \"Typo\", \"hills\": [{\"radius\": 5}, {\"raduis\": 5}]}";

            var error = Assert.Throws<System.InvalidOperationException>(() => MapSpecJson.Read(json));

            StringAssert.Contains("'hills[1].raduis' is not a map spec field", error.Message);
        }

        /// <summary>Asserts a spec's JSON loads, writes and reloads to the same content, and rewrites identically.</summary>
        public static void AssertRoundTrips(string json, string name)
        {
            var loaded = MapSpecJson.Read(json);
            var written = MapSpecJson.Write(loaded);
            var reloaded = MapSpecJson.Read(written);

            Assert.IsTrue(MapSpecJson.SameContent(loaded, reloaded), $"{name} changed in a round trip.");
            Assert.AreEqual(written, MapSpecJson.Write(reloaded), $"{name} writes differently the second time.");
        }

        private static MapSpec Sample()
        {
            var spec = new MapSpec
            {
                name = "Sample", symmetry = "rotate-180", size = new MapPoint(400, 300),
                notes = new List<string> { "first" },
            };
            spec.spawns.Add(new MapSpawnSpec { pos = new MapPoint(-55, -165.5f), yaw = 20 });
            spec.rivers.Add(new MapRiverSpec { points = { new MapPoint(-200, 0), new MapPoint(200, 10) } });
            spec.hills.Add(new MapHillSpec { pos = new MapPoint(30, -40), radius = 25 });
            spec.woods.Add(new MapWoodSpec { kind = "Oak", points = { new(0, 0), new(10, 0), new(5, -10) } });
            return spec;
        }
    }
}
