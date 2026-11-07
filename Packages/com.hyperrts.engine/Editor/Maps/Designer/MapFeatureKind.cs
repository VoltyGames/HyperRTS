using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// One feature list of <see cref="MapSpec"/> as the Map Editor sees it: label, geometry, the fields holding its
    /// position, points and radius, and its colour.
    /// </summary>
    public sealed class MapFeatureKind
    {
        private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        private readonly FieldInfo _list;
        private readonly FieldInfo _radius;
        private readonly FieldInfo _kind;
        private readonly FieldInfo _name;

        /// <summary>The kind of the <see cref="MapSpec"/> list <paramref name="list"/>, as its entry class's <see cref="MapFeatureAttribute"/> describes it.</summary>
        public MapFeatureKind(FieldInfo list)
        {
            _list = list;
            Field = list.Name;
            Label = char.ToUpperInvariant(Field[0]) + Field[1..];
            ElementType = list.FieldType.GetGenericArguments()[0];
            var feature = MapFeatureAttribute.Require(ElementType);
            Color = feature.Color;
            Noun = char.ToUpperInvariant(feature.Name[0]) + feature.Name[1..];
            Order = feature.Order;
            Geometry = FindGeometry(feature);
            _radius = Geometry == MapFeatureGeometry.Circle ? Require(feature.Radius, typeof(float)) : null;
            // Every spec class that has one names it alike.
            _kind = Optional(nameof(MapPropSpec.kind), typeof(string));
            _name = Optional(nameof(MapLookSpec.name), typeof(string));
            PositionField = HasPoints ? null : Single(typeof(MapPoint)).Name;
            PointsField = HasPoints ? Single(typeof(List<MapPoint>)).Name : null;
            RadiusField = _radius?.Name;
        }

        /// <summary>The list field on <see cref="MapSpec"/> (spawns, roads...).</summary>
        public string Field { get; }

        public string Label { get; }

        public MapFeatureGeometry Geometry { get; }

        public Color Color { get; }

        /// <summary>One feature of the list in singular ("Road"), for a feature with no name or kind.</summary>
        public string Noun { get; }

        /// <summary>The glyph class suffix of the kind's geometry, for the layers panel.</summary>
        public string Glyph => Geometry switch
        {
            MapFeatureGeometry.Point => "map-pin",
            MapFeatureGeometry.Circle => "circle",
            MapFeatureGeometry.Polyline => "spline",
            _ => "square-dashed",
        };

        /// <summary>The class of the list's entries (MapRoadSpec...).</summary>
        public Type ElementType { get; }

        /// <summary>The MapPoint field of Point and Circle features, or null.</summary>
        public string PositionField { get; }

        /// <summary>The List&lt;MapPoint&gt; field of Polyline and Polygon features, or null.</summary>
        public string PointsField { get; }

        /// <summary>The radius field of Circle features, or null.</summary>
        public string RadiusField { get; }

        public bool HasPoints => typeof(IMapLine).IsAssignableFrom(ElementType);

        /// <summary>Features of a higher order draw over those of a lower one.</summary>
        public int Order { get; }

        public int Count(MapSpec spec) => List(spec).Count;

        /// <summary>The feature's points in metres: its position, or its polyline or polygon points.</summary>
        public void ReadPoints(MapSpec spec, int index, List<Vector2> into)
        {
            into.Clear();
            switch (List(spec)[index])
            {
                case IMapLine line:
                    into.AddRange(line.Points.Select(point => point.ToVector2()));
                    return;
                case IMapPoint point:
                    into.Add(point.Position.ToVector2());
                    return;
            }
        }

        public float Radius(MapSpec spec, int index) => _radius == null ? 0f : (float)_radius.GetValue(List(spec)[index]);

        /// <summary>Shared features are placed once, not copied by the symmetry.</summary>
        public bool IsShared(MapSpec spec, int index) => List(spec)[index] is MapFeatureSpec { shared: true };

        /// <summary>A list row's label: the feature's name, else its kind, else the kind's noun, then its centre in metres.</summary>
        public string Describe(MapSpec spec, int index)
        {
            var element = List(spec)[index];
            var name = Text(_name, element) ?? Text(_kind, element) ?? Noun;
            var points = new List<Vector2>();
            ReadPoints(spec, index, points);
            var centre = points.Count == 0 ? Vector2.zero : points.Aggregate(Vector2.zero, (sum, p) => sum + p) / points.Count;
            return FormattableString.Invariant($"{name}  ({centre.x:0}, {centre.y:0})");
        }

        private static string Text(FieldInfo field, object element)
        {
            var text = (string)field?.GetValue(element);
            return string.IsNullOrEmpty(text) ? null : text;
        }

        /// <summary>
        /// Appends a feature at <paramref name="points"/> and returns its index. It copies the list's last feature,
        /// or takes the type's defaults in an empty list.
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

        private static void WritePoints(object element, IReadOnlyList<Vector2> points)
        {
            if (element is IMapPoint point)
            {
                point.Position = new MapPoint(points[0].x, points[0].y);
                return;
            }

            var line = ((IMapLine)element).Points;
            line.Clear();
            line.AddRange(points.Select(p => new MapPoint(p.x, p.y)));
        }

        private void ClearLists(object element)
        {
            foreach (var field in ElementType.GetFields(Public))
            {
                var type = field.FieldType;
                if (UnitySerialization.ItemType(type) != null)
                {
                    field.SetValue(element, Activator.CreateInstance(type));
                }
            }
        }

        private IList List(MapSpec spec) => (IList)_list.GetValue(spec)
            ?? throw new InvalidOperationException($"Spec '{spec.name}' has no '{Field}' list.");

        private MapFeatureGeometry FindGeometry(MapFeatureAttribute feature)
        {
            if (HasPoints)
            {
                return feature.Closed ? MapFeatureGeometry.Polygon : MapFeatureGeometry.Polyline;
            }

            if (!typeof(IMapPoint).IsAssignableFrom(ElementType))
            {
                throw new ArgumentException($"{ElementType.Name} for '{Field}' is neither an IMapPoint nor an IMapLine.");
            }

            return feature.Radius == null ? MapFeatureGeometry.Point : MapFeatureGeometry.Circle;
        }

        private FieldInfo Single(Type type)
        {
            var fields = ElementType.GetFields(Public).Where(field => field.FieldType == type).ToArray();
            if (fields.Length != 1)
            {
                throw new ArgumentException($"{ElementType.Name} needs exactly one {type.Name} field for '{Field}', not {fields.Length}.");
            }

            return fields[0];
        }

        private FieldInfo Require(string name, Type type)
        {
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
