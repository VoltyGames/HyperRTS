using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>What every canvas tool works on: the document, the selection, the canvas view and the symmetry.</summary>
    public sealed class MapToolContext
    {
        public MapToolContext(MapSpecDocument document, MapFeatureSelection selection, MapCanvasView view)
        {
            Document = document;
            Selection = selection;
            View = view;
        }

        public MapSpecDocument Document { get; }

        public MapFeatureSelection Selection { get; }

        public MapCanvasView View { get; }

        /// <summary>The spec's symmetry, or null while it names an unknown one.</summary>
        public MapSymmetry Symmetry { get; set; }

        /// <summary>A feature position under the cursor: snapped, and onto the axis or centre when shared.</summary>
        public Vector2 Place(Vector2 local, bool shared) => MapSnap.Position(View.ToWorld(local), shared, Symmetry);
    }
}
