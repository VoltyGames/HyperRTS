using System;
using HyperRTS.Editor.Maps.Designer.Canvas;
using HyperRTS.Editor.Maps.Designer.Features;
using HyperRTS.Editor.Maps.Designer.Window;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Validation.Rules.Fields;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer.Tools
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

        /// <summary>Which categories the canvas shows and lets tools pick; the canvas sets it when it binds one.</summary>
        public MapLayers Layers { get; set; } = new();

        public MapCanvasView View { get; }

        /// <summary>Shows a passing warning over the canvas, such as what a deletion changed.</summary>
        public Action<string> Warn { get; }

        /// <summary>The spec's symmetry, or null while it names an unknown one.</summary>
        public MapSymmetry Symmetry { get; set; }

        /// <summary>A feature position under the cursor: snapped, and onto the axis or centre when shared.</summary>
        public Vector2 Place(Vector2 local, bool shared) => MapSnap.Position(View.ToWorld(local), shared, Symmetry);
    }
}
