using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's Select/Move tool: click selects, dragging a feature moves it, dragging a handle moves that
    /// vertex (or a circle's radius), Delete removes the selection. Each drag is one undo step.
    /// </summary>
    public sealed class MapSelectTool
    {
        private const float Snap = 0.5f;
        private const float MinRadius = 1f;
        private const int WholeFeature = -1;

        private readonly MapSpecDocument _document;
        private readonly MapFeatureSelection _selection;
        private readonly MapCanvasView _view;
        private readonly List<Vector2> _start = new();
        private readonly List<Vector2> _moved = new();

        private bool _dragging;
        private int _handle;
        private Vector2 _grab;
        private int _undoGroup;

        public MapSelectTool(MapSpecDocument document, MapFeatureSelection selection, MapCanvasView view)
        {
            _document = document;
            _selection = selection;
            _view = view;
        }

        public bool Dragging => _dragging;

        public void Down(Vector2 local)
        {
            var spec = _document.spec;
            var current = _selection.Current;
            _handle = current.IsNone ? -1 : MapHitTest.HandleAt(spec, _view, current, local);
            if (_handle < 0)
            {
                _selection.Set(MapHitTest.FeatureAt(spec, _view, local));
                _handle = WholeFeature;
            }

            if (_selection.Current.IsNone)
            {
                return;
            }

            MapHitTest.Handles(spec, _selection.Current, _start);
            _grab = _view.ToWorld(local);
            _dragging = true;
            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
        }

        public void Move(Vector2 local)
        {
            if (!_dragging)
            {
                return;
            }

            var world = _view.ToWorld(local);
            if (_handle == WholeFeature)
            {
                MoveFeature(Round(world - _grab));
                return;
            }

            MoveHandle(Round(world));
        }

        public void Up()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            Undo.CollapseUndoOperations(_undoGroup);
        }

        public void DeleteSelected()
        {
            var current = _selection.Current;
            if (current.IsNone || _dragging)
            {
                return;
            }

            _selection.Clear();
            MapFeatureEdits.Delete(_document, current);
        }

        private void MoveFeature(Vector2 delta)
        {
            var feature = _selection.Current;
            var points = feature.Kind.HasPoints ? _start.Count : 1;
            _moved.Clear();
            for (var i = 0; i < points; i++)
            {
                _moved.Add(_start[i] + delta);
            }

            MapFeatureEdits.SetPoints(_document, feature, _moved);
        }

        private void MoveHandle(Vector2 world)
        {
            var feature = _selection.Current;
            var radiusHandle = feature.Kind.Geometry == MapFeatureGeometry.Circle && _handle == 1;
            if (radiusHandle)
            {
                var radius = Mathf.Max(MinRadius, Round(Vector2.Distance(world, _start[0])));
                MapFeatureEdits.SetRadius(_document, feature, radius);
                return;
            }

            var points = feature.Kind.HasPoints ? _start.Count : 1;
            _moved.Clear();
            for (var i = 0; i < points; i++)
            {
                _moved.Add(i == _handle ? world : _start[i]);
            }

            MapFeatureEdits.SetPoints(_document, feature, _moved);
        }

        private static Vector2 Round(Vector2 value) => new(Round(value.x), Round(value.y));

        private static float Round(float value) => Mathf.Round(value / Snap) * Snap;
    }
}
