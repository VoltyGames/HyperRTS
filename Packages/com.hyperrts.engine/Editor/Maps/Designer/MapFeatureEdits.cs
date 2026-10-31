using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Undoable edits to one feature of a <see cref="MapSpecDocument"/>, made through its SerializedObject. An edit
    /// passed <c>finished: false</c> is part of a drag, which saves once it ends.
    /// </summary>
    public static class MapFeatureEdits
    {
        /// <summary>
        /// Sets the feature's points: its single position (Point, Circle) or its whole point list (Polyline, Polygon),
        /// which must keep its length.
        /// </summary>
        public static void SetPoints(MapSpecDocument document, MapFeatureRef feature, IReadOnlyList<Vector2> points,
            bool finished)
        {
            var element = Element(document, feature);
            if (!feature.Kind.HasPoints)
            {
                Write(element.FindPropertyRelative(feature.Kind.PositionField), points[0]);
                document.Apply($"Move {feature}", finished);
                return;
            }

            var list = element.FindPropertyRelative(feature.Kind.PointsField);
            for (var i = 0; i < points.Count; i++)
            {
                Write(list.GetArrayElementAtIndex(i), points[i]);
            }

            document.Apply($"Move {feature}", finished);
        }

        public static void SetRadius(MapSpecDocument document, MapFeatureRef feature, float radius, bool finished)
        {
            Element(document, feature).FindPropertyRelative(feature.Kind.RadiusField).floatValue = radius;
            document.Apply($"Resize {feature}", finished);
        }

        /// <summary>
        /// Appends a feature of <paramref name="kind"/> at <paramref name="points"/> as one undo step and returns it.
        /// See <see cref="MapFeatureKind.Add"/> for the fields it starts with.
        /// </summary>
        public static MapFeatureRef Add(MapSpecDocument document, MapFeatureKind kind, IReadOnlyList<Vector2> points)
        {
            var index = -1;
            Undo.IncrementCurrentGroup();
            document.Edit($"Add {kind.Label}", spec => index = kind.Add(spec, points));
            return new MapFeatureRef(kind, index);
        }

        /// <summary>Inserts a vertex into a line or area at <paramref name="index"/> (up to its point count).</summary>
        public static void InsertPoint(MapSpecDocument document, MapFeatureRef feature, int index, Vector2 point,
            bool finished)
        {
            var list = Element(document, feature).FindPropertyRelative(feature.Kind.PointsField);
            if (index == list.arraySize)
            {
                list.arraySize++;
            }
            else
            {
                list.InsertArrayElementAtIndex(index);
            }

            Write(list.GetArrayElementAtIndex(index), point);
            document.Apply($"Add vertex to {feature}", finished);
        }

        public static void RemovePoint(MapSpecDocument document, MapFeatureRef feature, int index)
        {
            var list = Element(document, feature).FindPropertyRelative(feature.Kind.PointsField);
            list.DeleteArrayElementAtIndex(index);
            document.Apply($"Remove vertex from {feature}");
        }

        /// <summary>
        /// Deletes the feature. False when it was the last of its kind; true when later ones moved up a place, which
        /// changes their details, since a feature's place in its list seeds them.
        /// </summary>
        public static bool Delete(MapSpecDocument document, MapFeatureRef feature)
        {
            var serialized = document.Serialized;
            serialized.Update();
            var list = serialized.FindProperty($"{MapSpecDocument.SpecField}.{feature.Kind.Field}");
            var shifted = feature.Index < list.arraySize - 1;
            list.DeleteArrayElementAtIndex(feature.Index);
            document.Apply($"Delete {feature}");
            return shifted;
        }

        private static SerializedProperty Element(MapSpecDocument document, MapFeatureRef feature)
        {
            var serialized = document.Serialized;
            serialized.Update();
            return serialized.FindProperty(feature.PropertyPath);
        }

        private static void Write(SerializedProperty point, Vector2 value)
        {
            point.FindPropertyRelative(nameof(Spec.MapPoint.x)).floatValue = value.x;
            point.FindPropertyRelative(nameof(Spec.MapPoint.z)).floatValue = value.y;
        }
    }
}
