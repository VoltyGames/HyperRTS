using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Maps.Designer;
using HyperRTS.Editor.Maps.Spec;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>The Map Editor's feature table covers every spec feature list, and its geometry helpers.</summary>
    public class MapFeatureKindsTests
    {
        [Test]
        public void EveryPlacedFeatureListHasAKind()
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
        }

        [Test]
        public void KindsAreUnique()
        {
            var fields = MapFeatureKinds.All.Select(kind => kind.Field).ToList();
            Assert.AreEqual(fields.Count, fields.Distinct().Count());
        }

        [Test]
        public void ReadsPointsAndRadius()
        {
            var spec = new MapSpec();
            spec.settlements.Add(new MapSettlementSpec { center = new MapPoint(5, 6), radius = 40 });
            spec.roads.Add(new MapRoadSpec { points = { new MapPoint(1, 2), new MapPoint(3, 4) } });
            var points = new List<Vector2>();

            MapFeatureKinds.Find("settlements").ReadPoints(spec, 0, points);
            Assert.AreEqual(new[] { new Vector2(5, 6) }, points);
            Assert.AreEqual(40f, MapFeatureKinds.Find("settlements").Radius(spec, 0));

            MapFeatureKinds.Find("roads").ReadPoints(spec, 0, points);
            Assert.AreEqual(new[] { new Vector2(1, 2), new Vector2(3, 4) }, points);
        }

        [Test]
        public void HitTestFindsPolygonInteriorAndPointsOnTop()
        {
            var spec = new MapSpec();
            spec.woods.Add(new MapWoodSpec { points = { new(-50, -50), new(50, -50), new(50, 50), new(-50, 50) } });
            spec.props.Add(new MapPropSpec { pos = new MapPoint(10, 10) });
            var view = new MapCanvasView { Size = new Vector2(200, 200) };

            Assert.AreEqual(new MapFeatureRef(MapFeatureKinds.Find("props"), 0),
                MapHitTest.FeatureAt(spec, view, view.ToLocal(new Vector2(10, 10))));
            Assert.AreEqual(new MapFeatureRef(MapFeatureKinds.Find("woods"), 0),
                MapHitTest.FeatureAt(spec, view, view.ToLocal(new Vector2(-30, 20))));
            Assert.IsTrue(MapHitTest.FeatureAt(spec, view, view.ToLocal(new Vector2(90, 90))).IsNone);
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
