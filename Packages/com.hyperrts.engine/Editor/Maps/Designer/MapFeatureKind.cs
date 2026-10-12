using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// One feature list of <see cref="MapSpec"/> as the Map Editor sees it: label, geometry, the fields holding its
    /// position, points and radius, and its colour. Fields are resolved by reflection and must exist.
    /// </summary>
    public sealed class MapFeatureKind
    {
        private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        private readonly FieldInfo _list;
        private readonly FieldInfo _position;
        private readonly FieldInfo _points;
        private readonly FieldInfo _radius;
        private readonly FieldInfo _shared;
        private readonly FieldInfo _kind;

        private MapFeatureKind(string field, string label, MapFeatureGeometry geometry, Color color,
            string positionField, string pointsField, string radiusField)
        {
            Field = field;
            Label = label;
            Geometry = geometry;
            Color = color;
            PositionField = positionField;
            PointsField = pointsField;
            RadiusField = radiusField;

            _list = typeof(MapSpec).GetField(field, Public)
                ?? throw new ArgumentException($"MapSpec has no feature list '{field}'.");
            ElementType = _list.FieldType.GetGenericArguments()[0];
            _position = Require(positionField, typeof(MapPoint));
            _points = Require(pointsField, typeof(List<MapPoint>));
            _radius = Require(radiusField, typeof(float));
            _shared = Optional("shared", typeof(bool));
            _kind = Optional("kind", typeof(string));
        }

        /// <summary>The list field on <see cref="MapSpec"/> (spawns, roads...).</summary>
        public string Field { get; }

        public string Label { get; }

        public MapFeatureGeometry Geometry { get; }

        public Color Color { get; }

        public Type ElementType { get; }

        /// <summary>The MapPoint field of Point and Circle features, or null.</summary>
        public string PositionField { get; }

        /// <summary>The List&lt;MapPoint&gt; field of Polyline and Polygon features, or null.</summary>
        public string PointsField { get; }

        /// <summary>The radius field of Circle features, or null.</summary>
        public string RadiusField { get; }

        public bool HasPoints => _points != null;

        public static MapFeatureKind Point(string field, string label, Color color, string position = "pos") =>
            new(field, label, MapFeatureGeometry.Point, color, position, null, null);

        public static MapFeatureKind Circle(string field, string label, Color color, string position, string radius) =>
            new(field, label, MapFeatureGeometry.Circle, color, position, null, radius);

        public static MapFeatureKind Polyline(string field, string label, Color color) =>
            new(field, label, MapFeatureGeometry.Polyline, color, null, "points", null);

        public static MapFeatureKind Polygon(string field, string label, Color color) =>
            new(field, label, MapFeatureGeometry.Polygon, color, null, "points", null);

        public int Count(MapSpec spec) => List(spec).Count;

        /// <summary>The feature's points in metres: its position, or its polyline or polygon points.</summary>
        public void ReadPoints(MapSpec spec, int index, List<Vector2> into)
        {
            into.Clear();
            var element = List(spec)[index];
            if (_points == null)
            {
                into.Add(((MapPoint)_position.GetValue(element)).ToVector2());
                return;
            }

            foreach (var point in (List<MapPoint>)_points.GetValue(element))
            {
                into.Add(point.ToVector2());
            }
        }

        public float Radius(MapSpec spec, int index) => _radius == null ? 0f : (float)_radius.GetValue(List(spec)[index]);

        /// <summary>Shared features are placed once, not copied by the symmetry.</summary>
        public bool IsShared(MapSpec spec, int index) => _shared != null && (bool)_shared.GetValue(List(spec)[index]);

        /// <summary>A list row's name: its number and its palette or climate kind.</summary>
        public string Describe(MapSpec spec, int index)
        {
            var kind = _kind == null ? null : (string)_kind.GetValue(List(spec)[index]);
            return string.IsNullOrEmpty(kind) ? $"{index + 1}" : $"{index + 1}  {kind}";
        }

        /// <summary>
        /// Appends a feature at <paramref name="points"/> and returns its index. It copies the list's last feature
        /// (kind, width, shared...) or, in an empty list, takes the type's defaults. Lists on the feature other than
        /// its points (a river's crossings) start empty: they belong to the old feature's geometry.
        /// </summary>
        public int Add(MapSpec spec, IReadOnlyList<Vector2> points)
        {
            var list = List(spec);
            var element = list.Count == 0
                ? Activator.CreateInstance(ElementType)
                : JsonUtility.FromJson(JsonUtility.ToJson(list[list.Count - 1]), ElementType);
            ClearLists(element);
            WritePoints(element, points);
            list.Add(element);
            return list.Count - 1;
        }

        private void WritePoints(object element, IReadOnlyList<Vector2> points)
        {
            if (_points == null)
            {
                _position.SetValue(element, new MapPoint(points[0].x, points[0].y));
                return;
            }

            var list = new List<MapPoint>();
            foreach (var point in points)
            {
                list.Add(new MapPoint(point.x, point.y));
            }

            _points.SetValue(element, list);
        }

        private void ClearLists(object element)
        {
            foreach (var field in ElementType.GetFields(Public))
            {
                var type = field.FieldType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    field.SetValue(element, Activator.CreateInstance(type));
                }
            }
        }

        private IList List(MapSpec spec) => (IList)_list.GetValue(spec)
            ?? throw new InvalidOperationException($"Spec '{spec.name}' has no '{Field}' list.");

        private FieldInfo Require(string name, Type type)
        {
            if (name == null)
            {
                return null;
            }

            var field = ElementType.GetField(name, Public);
            if (field == null || field.FieldType != type)
            {
                throw new ArgumentException($"{ElementType.Name} has no {type.Name} field '{name}' for '{Field}'.");
            }

            return field;
        }

        private FieldInfo Optional(string name, Type type)
        {
            var field = ElementType.GetField(name, Public);
            return field != null && field.FieldType == type ? field : null;
        }
    }
}
