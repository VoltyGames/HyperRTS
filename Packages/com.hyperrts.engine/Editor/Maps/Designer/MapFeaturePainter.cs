using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Symmetry;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>Draws a spec's features on the Map Editor canvas by their kind's geometry and colour.</summary>
    public static class MapFeaturePainter
    {
        private const float LineWidth = 2f;
        private const float SelectedWidth = 3.5f;
        private const float PointRadius = 5f;
        private const float HandleHalf = 4f;
        private const float GhostAlpha = 0.3f;
        private const float FillAlpha = 0.18f;

        private static readonly Color Outline = new(0f, 0f, 0f, 0.7f);
        private static readonly Color Selected = Color.white;
        [NoAutoStaticsCleanup]
        private static readonly List<Vector2> World = new();
        [NoAutoStaticsCleanup]
        private static readonly List<Vector2> Local = new();

        /// <summary>The symmetry's other copies of every feature that isn't shared, faded.</summary>
        public static void DrawGhosts(Painter2D painter, MapSpec spec, MapCanvasView view, MapSymmetry symmetry)
        {
            foreach (var kind in MapFeatureKinds.All)
            {
                var color = Fade(kind.Color, GhostAlpha);
                for (var i = 0; i < kind.Count(spec); i++)
                {
                    if (kind.IsShared(spec, i))
                    {
                        continue;
                    }

                    kind.ReadPoints(spec, i, World);
                    for (var copy = 1; copy < symmetry.Copies.Count; copy++)
                    {
                        ToLocal(view, World, symmetry.Copies[copy]);
                        DrawShape(painter, kind.Geometry, kind.Radius(spec, i) * view.Scale, color, LineWidth);
                    }
                }
            }
        }

        public static void DrawFeatures(Painter2D painter, MapSpec spec, MapCanvasView view)
        {
            foreach (var kind in MapFeatureKinds.All)
            {
                for (var i = 0; i < kind.Count(spec); i++)
                {
                    kind.ReadPoints(spec, i, World);
                    ToLocal(view, World, MapTransform.Identity);
                    DrawShape(painter, kind.Geometry, kind.Radius(spec, i) * view.Scale, kind.Color, LineWidth);
                }
            }
        }

        /// <summary>The selected feature outlined in white.</summary>
        public static void DrawSelection(Painter2D painter, MapSpec spec, MapCanvasView view, MapFeatureRef selected)
        {
            if (selected.IsNone)
            {
                return;
            }

            var kind = selected.Kind;
            kind.ReadPoints(spec, selected.Index, World);
            ToLocal(view, World, MapTransform.Identity);
            DrawShape(painter, kind.Geometry, kind.Radius(spec, selected.Index) * view.Scale, Selected, SelectedWidth);

            MapHitTest.Handles(spec, selected, World);
            ToLocal(view, World, MapTransform.Identity);
            foreach (var handle in Local)
            {
                Square(painter, handle);
            }
        }

        private static void Square(Painter2D painter, Vector2 centre)
        {
            painter.BeginPath();
            painter.MoveTo(centre + new Vector2(-HandleHalf, -HandleHalf));
            painter.LineTo(centre + new Vector2(HandleHalf, -HandleHalf));
            painter.LineTo(centre + new Vector2(HandleHalf, HandleHalf));
            painter.LineTo(centre + new Vector2(-HandleHalf, HandleHalf));
            painter.ClosePath();
            painter.fillColor = Selected;
            painter.Fill();
            painter.strokeColor = Outline;
            painter.lineWidth = 1f;
            painter.Stroke();
        }

        private static void DrawShape(Painter2D painter, MapFeatureGeometry geometry, float radius, Color color,
            float width)
        {
            switch (geometry)
            {
                case MapFeatureGeometry.Point:
                    Disc(painter, Local[0], PointRadius, color);
                    return;
                case MapFeatureGeometry.Circle:
                    Ring(painter, Local[0], radius, color, width);
                    Disc(painter, Local[0], PointRadius * 0.6f, color);
                    return;
                default:
                    Path(painter, geometry == MapFeatureGeometry.Polygon, color, width);
                    return;
            }
        }

        private static void Path(Painter2D painter, bool closed, Color color, float width)
        {
            if (Local.Count < 2)
            {
                return;
            }

            painter.BeginPath();
            painter.MoveTo(Local[0]);
            for (var i = 1; i < Local.Count; i++)
            {
                painter.LineTo(Local[i]);
            }

            if (closed)
            {
                painter.ClosePath();
                painter.fillColor = Fade(color, color.a * FillAlpha);
                painter.Fill();
            }

            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
        }

        /// <summary>Starts a closed circular path for the caller to fill or stroke.</summary>
        internal static void Circle(Painter2D painter, Vector2 centre, float radius)
        {
            painter.BeginPath();
            painter.Arc(centre, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.ClosePath();
        }

        private static void Ring(Painter2D painter, Vector2 centre, float radius, Color color, float width)
        {
            Circle(painter, centre, radius);
            painter.fillColor = Fade(color, color.a * FillAlpha);
            painter.Fill();
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.Stroke();
        }

        private static void Disc(Painter2D painter, Vector2 centre, float radius, Color color)
        {
            Circle(painter, centre, radius);
            painter.fillColor = color;
            painter.Fill();
            painter.strokeColor = Fade(Outline, Outline.a * color.a);
            painter.lineWidth = 1f;
            painter.Stroke();
        }

        private static void ToLocal(MapCanvasView view, List<Vector2> world, MapTransform transform)
        {
            Local.Clear();
            foreach (var point in world)
            {
                Local.Add(view.ToLocal(transform.Apply(point)));
            }
        }

        private static Color Fade(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
