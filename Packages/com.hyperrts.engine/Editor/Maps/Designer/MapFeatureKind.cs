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
    /// position, points and radius, and its colour. Callers name fields with <c>nameof</c>, so they always exist.
    /// </summary>
    public sealed class MapFeatureKind
    {
        private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        private readonly FieldInfo _list;
        private readonly Type _element;
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

            _list = typeof(MapSpec).GetField(field, Public)
                ?? throw new ArgumentException($"MapSpec has no feature list '{field}'.");
            _element = _list.FieldType.GetGenericArguments()[0];
            _position = Require(positionField, typeof(MapPoint));
            _points = Require(pointsField, typeof(List<MapPoint>));
            _radius = Require(radiusField, typeof(float));
            // Every spec class that has these names them alike.
            _shared = Optional(nameof(MapPropSpec.shared), typeof(bool));
            _kind = Optional(nameof(MapPropSpec.kind), typeof(string));
        }

        /// <summary>The list field on <see cref="MapSpec"/> (spawns, roads...).</summary>
        public string Field { get; }

        public string Label { get; }

        public MapFeatureGeometry Geometry { get; }

        public Color Color { get; }

        public static MapFeatureKind Point(string field, string label, Color color, string position) =>
            new(field, label, MapFeatureGeometry.Point, color, position, null, null);

        public static MapFeatureKind Circle(string field, string label, Color color, string position, string radius) =>
            new(field, label, MapFeatureGeometry.Circle, color, position, null, radius);

        public static MapFeatureKind Polyline(string field, string label, Color color, string points) =>
            new(field, label, MapFeatureGeometry.Polyline, color, null, points, null);

        public static MapFeatureKind Polygon(string field, string label, Color color, string points) =>
            new(field, label, MapFeatureGeometry.Polygon, color, null, points, null);

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

        private IList List(MapSpec spec) => (IList)_list.GetValue(spec)
            ?? throw new InvalidOperationException($"Spec '{spec.name}' has no '{Field}' list.");

        private FieldInfo Require(string name, Type type)
        {
            if (name == null)
            {
                return null;
            }

            var field = _element.GetField(name, Public);
            if (field == null || field.FieldType != type)
            {
                throw new ArgumentException($"{_element.Name} has no {type.Name} field '{name}' for '{Field}'.");
            }

            return field;
        }

        private FieldInfo Optional(string name, Type type)
        {
            var field = _element.GetField(name, Public);
            return field != null && field.FieldType == type ? field : null;
        }
    }
}
