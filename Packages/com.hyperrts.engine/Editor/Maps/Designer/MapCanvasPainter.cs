using HyperRTS.Editor.Maps.Layout;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor canvas's background lines: metre grid, playable bounds, backdrop edge and sector edges.</summary>
    public static class MapCanvasPainter
    {
        private const float MinGridPixels = 40f;

        private static readonly float[] GridSteps = { 1f, 2f, 5f, 10f, 20f, 50f, 100f, 200f, 500f, 1000f };
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.08f);
        private static readonly Color BoundsColor = new(1f, 1f, 1f, 0.85f);
        private static readonly Color MarginColor = new(1f, 1f, 1f, 0.3f);
        private static readonly Color SectorColor = new(1f, 0.6f, 0.15f, 0.9f);

        /// <summary>The smallest grid step (metres) at least <see cref="MinGridPixels"/> apart at this zoom.</summary>
        public static float GridStep(MapCanvasView view)
        {
            foreach (var step in GridSteps)
            {
                if (step * view.Scale >= MinGridPixels)
                {
                    return step;
                }
            }

            return GridSteps[GridSteps.Length - 1];
        }

        public static void DrawGrid(Painter2D painter, MapCanvasView view, Rect terrain)
        {
            var step = GridStep(view);
            painter.strokeColor = GridColor;
            painter.lineWidth = 1f;
            painter.BeginPath();
            for (var x = Mathf.Ceil(terrain.xMin / step) * step; x <= terrain.xMax; x += step)
            {
                painter.MoveTo(view.ToLocal(new Vector2(x, terrain.yMin)));
                painter.LineTo(view.ToLocal(new Vector2(x, terrain.yMax)));
            }

            for (var z = Mathf.Ceil(terrain.yMin / step) * step; z <= terrain.yMax; z += step)
            {
                painter.MoveTo(view.ToLocal(new Vector2(terrain.xMin, z)));
                painter.LineTo(view.ToLocal(new Vector2(terrain.xMax, z)));
            }

            painter.Stroke();
        }

        public static void DrawBounds(Painter2D painter, MapCanvasView view, Rect playable, Rect terrain)
        {
            Outline(painter, view.ToLocal(terrain), MarginColor, 1f);
            Outline(painter, view.ToLocal(playable), BoundsColor, 1.5f);
        }

        /// <summary>The edges of the authored sector: the mirror axis, or the rays bounding a rotation's wedge.</summary>
        public static void DrawSector(Painter2D painter, MapCanvasView view, MapSymmetry symmetry, Rect playable)
        {
            var reach = Mathf.Max(playable.width, playable.height) * 0.5f;
            painter.strokeColor = SectorColor;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            switch (symmetry.Name)
            {
                case MapSymmetry.MirrorXName:
                    Line(painter, view, new Vector2(0f, -reach), new Vector2(0f, reach));
                    break;
                case MapSymmetry.MirrorZName:
                case MapSymmetry.Rotate180:
                    Line(painter, view, new Vector2(-reach, 0f), new Vector2(reach, 0f));
                    break;
                default:
                    var far = symmetry.Copies[1].Apply(Vector2.right) * reach;
                    Line(painter, view, Vector2.right * reach, Vector2.zero);
                    painter.LineTo(view.ToLocal(far));
                    break;
            }

            painter.Stroke();
        }

        private static void Line(Painter2D painter, MapCanvasView view, Vector2 from, Vector2 to)
        {
            painter.MoveTo(view.ToLocal(from));
            painter.LineTo(view.ToLocal(to));
        }

        private static void Outline(Painter2D painter, Rect rect, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Stroke();
        }
    }
}
