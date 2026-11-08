using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Common.Json;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Designer;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests
{
    /// <summary>The Map Editor: its feature table and canvas geometry, and its compact, lossless spec writer.</summary>
    public class MapEditorTests
    {
        [Test]
        public void EveryPlacedFeatureListHasOneKind()
        {
            var missing = new List<string>();
            foreach (var field in typeof(MapSpec).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsPlacedList(field) && MapFeatureKinds.All.All(kind => kind.Field != field.Name))
                {
                    missing.Add(field.Name);
                }
            }

            Assert.IsEmpty(missing, "MapFeatureKinds needs a row for: " + string.Join(", ", missing));
            Assert.That(MapFeatureKinds.All.Select(kind => kind.Field), Is.Unique);
            Assert.That(MapFeatureKinds.All.Select(kind => kind.Order), Is.Unique, "two kinds would draw in field order");
        }

        [Test]
        public void HitTestFindsPolygonInteriorAndPointsOnTop()
        {
            var spec = new MapSpec();
            spec.woods.Add(new MapWoodSpec { points = { new(-50, -50), new(50, -50), new(50, 50), new(-50, 50) } });
            spec.props.Add(new MapPropSpec { pos = new MapPoint(10, 10) });
            var view = new MapCanvasView { Size = new Vector2(200, 200) };

            Assert.AreEqual(new MapFeatureRef(MapFeatureKinds.Find("props"), 0),
                MapHitTest.FeatureAt(spec, view, new MapLayers(), view.ToLocal(new Vector2(10, 10))));
            Assert.AreEqual(new MapFeatureRef(MapFeatureKinds.Find("woods"), 0),
                MapHitTest.FeatureAt(spec, view, new MapLayers(), view.ToLocal(new Vector2(-30, 20))));
            Assert.IsTrue(MapHitTest.FeatureAt(spec, view, new MapLayers(), view.ToLocal(new Vector2(90, 90))).IsNone);
        }

        [Test]
        public void HitTestSkipsHiddenAndLockedCategories()
        {
            var spec = new MapSpec();
            spec.woods.Add(new MapWoodSpec { points = { new(-50, -50), new(50, -50), new(50, 50), new(-50, 50) } });
            spec.props.Add(new MapPropSpec { pos = new MapPoint(10, 10) });
            var view = new MapCanvasView { Size = new Vector2(200, 200) };
            var props = MapFeatureKinds.Find("props");
            var woods = new MapFeatureRef(MapFeatureKinds.Find("woods"), 0);
            var onProp = view.ToLocal(new Vector2(10, 10));
            var layers = new MapLayers();

            layers.SetLocked(props, true);
            Assert.AreEqual(woods, MapHitTest.FeatureAt(spec, view, layers, onProp));

            layers.SetLocked(props, false);
            layers.SetVisible(MapFeatureKinds.Find("woods"), false);
            layers.SetVisible(props, false);
            Assert.IsTrue(MapHitTest.FeatureAt(spec, view, layers, onProp).IsNone);
        }

        [Test]
        public void ZoomKeepsTheCursorPoint()
        {
            var view = new MapCanvasView { Size = new Vector2(400, 300) };
            view.Fit(new Rect(-100, -100, 200, 200));
            var cursor = new Vector2(310, 40);
            var before = view.ToWorld(cursor);

            view.ZoomAt(cursor, 1.5f);

            Assert.That(Vector2.Distance(before, view.ToWorld(cursor)), Is.LessThan(1e-3f));
            Assert.That(Vector2.Distance(cursor, view.ToLocal(before)), Is.LessThan(1e-3f));
        }

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
        public void MalformedJsonFailsLoudly()
        {
            Assert.Throws<System.FormatException>(() => JsonFormat.Format("{\"a\": [1, 2}"));
        }

        [Test]
        public void ANestedFieldTheSpecDoesNotRead_ThrowsNamingItsPath()
        {
            const string json = "{\"name\": \"Typo\", \"hills\": [{\"radius\": 5}, {\"raduis\": 5}]}";

            var error = Assert.Throws<System.InvalidOperationException>(() => MapSpecJson.Read(json));

            StringAssert.Contains("'hills[1].raduis' is not a map spec field", error.Message);
        }

        /// <summary>Asserts a spec's JSON loads, writes and reloads to the same content, and rewrites identically.</summary>
        private static void AssertRoundTrips(string json, string name)
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

        private static bool IsPlacedList(FieldInfo field)
        {
            var type = field.FieldType;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(List<>))
            {
                return false;
            }

            var element = type.GetGenericArguments()[0];
            return element.GetFields().Any(f => f.FieldType == typeof(MapPoint) || f.FieldType == typeof(List<MapPoint>));
        }
    }
}
