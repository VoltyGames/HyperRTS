using System;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>What every canvas tool works on: the document, the selection, the canvas view and the symmetry.</summary>
    public sealed class MapToolContext
    {
        public MapToolContext(MapCanvasView view, Action<string> warn)
        {
            View = view;
            Warn = warn;
        }

        /// <summary>The open spec; the canvas sets it with the selection when it binds one.</summary>
        public MapSpecDocument Document { get; set; }

        public MapFeatureSelection Selection { get; set; }

        public MapCanvasView View { get; }

        /// <summary>Shows a passing warning over the canvas, such as what a deletion changed.</summary>
        public Action<string> Warn { get; }

        /// <summary>The spec's symmetry, or null while it names an unknown one.</summary>
        public MapSymmetry Symmetry { get; set; }

        /// <summary>A feature position under the cursor: snapped, and onto the axis or centre when shared.</summary>
        public Vector2 Place(Vector2 local, bool shared) => MapSnap.Position(View.ToWorld(local), shared, Symmetry);
    }
}
