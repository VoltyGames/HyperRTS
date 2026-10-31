using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's Line and Area tools: each click adds a snapped vertex, with a rubber band to the cursor; Enter
    /// or a double-click finishes the feature (a line needs two points, an area three) and selects it, Escape drops
    /// it. The feature copies its list's last one like the Point tool does. One undo step per finished feature.
    /// </summary>
    public sealed class MapPathTool : IMapTool
    {
        private const float LineWidth = 2f;
        private const float VertexRadius = 3f;
        private const float ClosingAlpha = 0.4f;

        private readonly MapToolContext _context;
        private readonly MapToolbox _toolbox;
        private readonly List<Vector2> _points = new();
        private Vector2? _cursor;

        public MapPathTool(MapToolContext context, MapToolbox toolbox)
        {
            _context = context;
            _toolbox = toolbox;
        }

        public void Down(Vector2 local, EventModifiers modifiers, int clickCount)
        {
            if (clickCount >= 2)
            {
                Finish();
                return;
            }

            var point = _context.Place(local, false);
            var repeated = _points.Count > 0 && _points[_points.Count - 1] == point;
            if (!repeated)
            {
                _points.Add(point);
            }
        }

        public void Move(Vector2 local) => _cursor = local;

        public void Up()
        {
        }

        public bool Key(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Finish();
                    return true;
                case KeyCode.Escape when _points.Count > 0:
                    _points.Clear();
                    return true;
                default:
                    return false;
            }
        }

        public void Cancel()
        {
            _points.Clear();
            _cursor = null;
        }

        public void Paint(Painter2D painter)
        {
            if (_points.Count == 0)
            {
                return;
            }

            var kind = _toolbox.Kind;
            var view = _context.View;
            painter.strokeColor = kind.Color;
            painter.lineWidth = LineWidth;
            painter.BeginPath();
            painter.MoveTo(view.ToLocal(_points[0]));
            for (var i = 1; i < _points.Count; i++)
            {
                painter.LineTo(view.ToLocal(_points[i]));
            }

            var band = _cursor == null ? (Vector2?)null : view.ToLocal(_context.Place(_cursor.Value, false));
            if (band != null)
            {
                painter.LineTo(band.Value);
            }

            painter.Stroke();
            PaintClosing(painter, kind, band);
            PaintVertices(painter, kind.Color);
        }

        /// <summary>An area's edge back to its first point, faded, so the shape it will close into shows.</summary>
        private void PaintClosing(Painter2D painter, MapFeatureKind kind, Vector2? band)
        {
            if (kind.Geometry != MapFeatureGeometry.Polygon || band == null)
            {
                return;
            }

            var color = kind.Color;
            painter.strokeColor = new Color(color.r, color.g, color.b, ClosingAlpha);
            painter.BeginPath();
            painter.MoveTo(band.Value);
            painter.LineTo(_context.View.ToLocal(_points[0]));
            painter.Stroke();
        }

        private void PaintVertices(Painter2D painter, Color color)
        {
            painter.fillColor = color;
            foreach (var point in _points)
            {
                MapFeaturePainter.Circle(painter, _context.View.ToLocal(point), VertexRadius);
                painter.Fill();
            }
        }

        /// <summary>Adds the drawn feature once it has enough points; with too few, keeps drawing.</summary>
        private void Finish()
        {
            var kind = _toolbox.Kind;
            if (_points.Count < MapVertexRules.MinPoints(kind.Geometry))
            {
                return;
            }

            var added = MapFeatureEdits.Add(_context.Document, kind, _points);
            _points.Clear();
            _context.Selection.Set(added);
        }
    }
}
