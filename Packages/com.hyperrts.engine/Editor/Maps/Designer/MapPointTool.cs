using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's Point tool: each click adds a feature of the picked point or circle kind at the cursor and
    /// selects it. The new feature copies the list's last one (kind, amount, radius, shared...), else takes the type's
    /// defaults; a shared one lands on the symmetry axis or centre. One undo step per feature.
    /// </summary>
    public sealed class MapPointTool : IMapTool
    {
        private const float MarkerRadius = 6f;

        private readonly MapToolContext _context;
        private readonly MapToolbox _toolbox;
        private Vector2? _cursor;

        public MapPointTool(MapToolContext context, MapToolbox toolbox)
        {
            _context = context;
            _toolbox = toolbox;
        }

        public void Down(Vector2 local, EventModifiers modifiers, int clickCount)
        {
            var kind = _toolbox.Kind;
            var position = _context.Place(local, SharedLikeLast(kind));
            var added = MapFeatureEdits.Add(_context.Document, kind, new[] { position });
            _context.Selection.Set(added);
        }

        public void Move(Vector2 local) => _cursor = local;

        public void Up()
        {
        }

        public bool Key(KeyCode key) => false;

        public void Cancel() => _cursor = null;

        /// <summary>A ring where the next click puts the feature, after snapping.</summary>
        public void Paint(Painter2D painter)
        {
            if (_cursor == null)
            {
                return;
            }

            var kind = _toolbox.Kind;
            var world = _context.Place(_cursor.Value, SharedLikeLast(kind));
            painter.BeginPath();
            painter.Arc(_context.View.ToLocal(world), MarkerRadius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.strokeColor = kind.Color;
            painter.lineWidth = 1.5f;
            painter.Stroke();
        }

        /// <summary>New features copy the last one, so they are shared when it is.</summary>
        private bool SharedLikeLast(MapFeatureKind kind)
        {
            var spec = _context.Document.spec;
            var count = kind.Count(spec);
            return count > 0 && kind.IsShared(spec, count - 1);
        }
    }
}
