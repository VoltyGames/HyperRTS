using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>Undoable edits to one feature of a <see cref="MapSpecDocument"/>, made through its SerializedObject.</summary>
    public static class MapFeatureEdits
    {
        /// <summary>
        /// Sets the feature's points: its single position (Point, Circle) or its whole point list (Polyline, Polygon),
        /// which must keep its length.
        /// </summary>
        public static void SetPoints(MapSpecDocument document, MapFeatureRef feature, IReadOnlyList<Vector2> points)
        {
            var element = Element(document, feature);
            if (!feature.Kind.HasPoints)
            {
                Write(element.FindPropertyRelative(feature.Kind.PositionField), points[0]);
                document.Apply($"Move {feature}");
                return;
            }

            var list = element.FindPropertyRelative(feature.Kind.PointsField);
            for (var i = 0; i < points.Count; i++)
            {
                Write(list.GetArrayElementAtIndex(i), points[i]);
            }

            document.Apply($"Move {feature}");
        }

        public static void SetRadius(MapSpecDocument document, MapFeatureRef feature, float radius)
        {
            Element(document, feature).FindPropertyRelative(feature.Kind.RadiusField).floatValue = radius;
            document.Apply($"Resize {feature}");
        }

        public static void Delete(MapSpecDocument document, MapFeatureRef feature)
        {
            var list = document.Serialized.FindProperty($"{MapSpecDocument.SpecField}.{feature.Kind.Field}");
            list.DeleteArrayElementAtIndex(feature.Index);
            document.Apply($"Delete {feature}");
        }

        private static SerializedProperty Element(MapSpecDocument document, MapFeatureRef feature)
        {
            var serialized = document.Serialized;
            serialized.Update();
            return serialized.FindProperty(feature.PropertyPath);
        }

        private static void Write(SerializedProperty point, Vector2 value)
        {
            point.FindPropertyRelative("x").floatValue = value.x;
            point.FindPropertyRelative("z").floatValue = value.y;
        }
    }
}
