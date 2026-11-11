using System.Collections.Generic;
using HyperRTS.Editor.Maps.Designer.Canvas;
using HyperRTS.Editor.Maps.Designer.Features;
using HyperRTS.Editor.Maps.Designer.Window;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer.Tools
{
    /// <summary>
    /// The Map Editor's Select tool: click selects, drags move a feature or handle, Alt+click adds a vertex,
    /// Ctrl+click removes one, Delete removes the selection. Each gesture is one undo step.
    /// </summary>
    public sealed class MapSelectTool : IMapTool
    {
        public const string Help = "Select (V): click selects, drag moves, drag a handle to reshape. Alt+click the " +
            "selected line or area's edge adds a vertex; Ctrl+click a vertex removes it; Delete removes the feature.";

        private const float MinRadius = 1f;
        private const int WholeFeature = -1;

        private readonly MapToolContext _context;
        private readonly List<Vector2> _start = new();
        private readonly List<Vector2> _moved = new();

        private bool _dragging;
        private int _handle;
        private Vector2 _grab;
        private int _undoGroup;

        public MapSelectTool(MapToolContext context)
        {
            _context = context;
        }

        private MapSpecDocument Document => _context.Document;

        private MapFeatureRef Current => _context.Selection.Current;

        public void Down(Vector2 local, EventModifiers modifiers, int clickCount)
        {
            // A feature picked in the list stays selected when its category is locked, but the canvas can't edit it.
            var current = Current;
            var editable = !current.IsNone && _context.Layers.CanPick(current.Kind);
            var adding = (modifiers & EventModifiers.Alt) != 0;
            if (adding && editable && InsertVertex(current, local))
            {
                return;
            }

            _handle = editable ? MapHitTest.HandleAt(Document.Spec, _context.View, current, local) : -1;
            var removing = (modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
            if (removing && _handle >= 0)
            {
                RemoveVertex(current, _handle);
                return;
            }

            if (_handle < 0)
            {
                _context.Selection.Set(MapHitTest.FeatureAt(Document.Spec, _context.View, _context.Layers, local));
                _handle = WholeFeature;
            }

            if (Current.IsNone)
            {
                return;
            }

            StartUndoGroup();
            BeginDrag(local);
        }

        public void Move(Vector2 local)
        {
            if (!_dragging)
            {
                return;
            }

            var world = _context.View.ToWorld(local);
            if (_handle == WholeFeature)
            {
                MoveFeature(MapSnap.Grid(world - _grab));
                return;
            }

            MoveHandle(world);
        }

        public void Up()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            Undo.CollapseUndoOperations(_undoGroup);
            Document.Commit();
        }

        public bool Key(KeyCode key)
        {
            var delete = key == KeyCode.Delete || key == KeyCode.Backspace;
            if (!delete)
            {
                return false;
            }

            DeleteSelected();
            return true;
        }

        public void Cancel() => Up();

        public void Paint(Painter2D painter)
        {
        }

        private void DeleteSelected()
        {
            var current = Current;
            if (current.IsNone || _dragging)
            {
                return;
            }

            _context.Selection.Clear();
            if (MapFeatureEdits.Delete(Document, current))
            {
                _context.Warn($"Deleted {current}. Later {current.Kind.Label} moved up a place, so their details " +
                    "change (Undo restores them).");
            }
        }

        /// <summary>Adds a vertex where the cursor is on the selected feature's edge and starts dragging it.</summary>
        private bool InsertVertex(MapFeatureRef feature, Vector2 local)
        {
            var segment = MapHitTest.SegmentAt(Document.Spec, _context.View, feature, local);
            if (segment < 0)
            {
                return false;
            }

            StartUndoGroup();
            _handle = segment + 1;
            MapFeatureEdits.InsertPoint(Document, feature, _handle, _context.Place(local, false), false);
            BeginDrag(local);
            return true;
        }

        private void RemoveVertex(MapFeatureRef feature, int handle)
        {
            feature.Kind.ReadPoints(Document.Spec, feature.Index, _start);
            if (!MapVertexRules.CanRemove(feature.Kind.Geometry, _start.Count))
            {
                return;
            }

            MapFeatureEdits.RemovePoint(Document, feature, handle);
        }

        private void StartUndoGroup()
        {
            Undo.IncrementCurrentGroup();
            _undoGroup = Undo.GetCurrentGroup();
        }

        private void BeginDrag(Vector2 local)
        {
            MapHitTest.Handles(Document.Spec, Current, _start);
            _grab = _context.View.ToWorld(local);
            _dragging = true;
        }

        private void MoveFeature(Vector2 delta)
        {
            var feature = Current;
            _moved.Clear();
            if (!feature.Kind.HasPoints)
            {
                _moved.Add(MapSnap.Position(_start[0] + delta, IsShared(feature), _context.Symmetry));
                MapFeatureEdits.SetPoints(Document, feature, _moved, false);
                return;
            }

            foreach (var point in _start)
            {
                _moved.Add(point + delta);
            }

            MapFeatureEdits.SetPoints(Document, feature, _moved, false);
        }

        private void MoveHandle(Vector2 world)
        {
            var feature = Current;
            var radiusHandle = feature.Kind.Geometry == MapFeatureGeometry.Circle && _handle == 1;
            if (radiusHandle)
            {
                var radius = Mathf.Max(MinRadius, MapSnap.Grid(Vector2.Distance(world, _start[0])));
                MapFeatureEdits.SetRadius(Document, feature, radius, false);
                return;
            }

            var placed = MapSnap.Position(world, IsShared(feature) && !feature.Kind.HasPoints, _context.Symmetry);
            var points = feature.Kind.HasPoints ? _start.Count : 1;
            _moved.Clear();
            for (var i = 0; i < points; i++)
            {
                _moved.Add(i == _handle ? placed : _start[i]);
            }

            MapFeatureEdits.SetPoints(Document, feature, _moved, false);
        }

        private bool IsShared(MapFeatureRef feature) => feature.Kind.IsShared(Document.Spec, feature.Index);
    }
}
