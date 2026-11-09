using System;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Presentation.HUD;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's map view: preview image, grid, features, symmetry ghosts and handles. Middle-drag or
    /// Space+drag pans, the wheel zooms, F fits; other input goes to the active tool (<see cref="MapCanvasTools"/>).
    /// </summary>
    [UxmlElement]
    public sealed partial class MapCanvas : VisualElement
    {
        private MapSpecDocument _document;
        private MapFeatureSelection _selection;
        private MapLayers _layers;
        private readonly MapCanvasView _view = new();
        private readonly MapToolContext _context;
        private MapCanvasTools _tools;
        private readonly MapCanvasBackdrop _image;
        private readonly VisualElement _overlay;
        private readonly Label _error;

        private MapSymmetry _symmetry;
        private bool _fitted;
        private bool _ghosts = true;
        private (Vector2 Position, Color Color)? _marker;

        public MapCanvas()
        {
            focusable = true;
            EditorAssets.Layout(typeof(MapCanvas)).CloneTree(this);
            _image = this.Require<MapCanvasBackdrop>("image");
            _overlay = this.Require<VisualElement>("overlay");
            _error = this.Require<Label>("error");
            _context = new MapToolContext(_view, message => Warned?.Invoke(message));

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp);
        }

        /// <summary>A tool's passing warning, such as what a deletion changed.</summary>
        public event Action<string> Warned;

        /// <summary>The pointer moved over the map; the position is in metres.</summary>
        public event Action<Vector2> CursorMoved;

        /// <summary>The pan or zoom changed.</summary>
        public event Action ViewChanged;

        /// <summary>Pixels per metre.</summary>
        public float Zoom => _view.Scale;

        /// <summary>The distance between grid lines in metres.</summary>
        public float GridMetres => MapCanvasPainter.GridStep(_view);

        /// <summary>
        /// Shows <paramref name="document"/>, fitted, for the tools in <paramref name="toolbox"/> to edit, with
        /// <paramref name="selection"/> drawn and set by them and <paramref name="layers"/> deciding what is drawn and picked.
        /// </summary>
        public void Bind(MapSpecDocument document, MapFeatureSelection selection, MapToolbox toolbox, MapLayers layers)
        {
            var first = _document == null;
            _tools?.Active.Cancel();
            _document = document;
            _selection = selection;
            _layers = layers;
            _marker = null;
            _context.Document = document;
            _context.Selection = selection;
            _context.Layers = layers;
            _image.Follow(_document.MapName);
            _fitted = false;
            if (first)
            {
                _tools = new MapCanvasTools(_context, toolbox, _overlay.MarkDirtyRepaint);
                _tools.Attach();
                RegisterCallback<AttachToPanelEvent>(_ => _tools.Attach());
                RegisterCallback<DetachFromPanelEvent>(_ => _tools.Detach());
                StartDrawing();
            }

            Refresh();
            OnResized();
        }

        // Painting and layout read the document, so they start with the first one.
        private void StartDrawing()
        {
            _overlay.generateVisualContent = Paint;
            RegisterCallback<GeometryChangedEvent>(_ => OnResized());
        }

        public bool ShowGhosts
        {
            get => _ghosts;
            set
            {
                _ghosts = value;
                _overlay.MarkDirtyRepaint();
            }
        }

        private Rect PlayableBounds
        {
            get
            {
                var size = _document.Spec.size.ToVector2();
                return new Rect(-size * 0.5f, size);
            }
        }

        private Rect TerrainBounds
        {
            get
            {
                var backdrop = _document.Spec.terrain.backdropWidth;
                var playable = PlayableBounds;
                return new Rect(playable.min - Vector2.one * backdrop, playable.size + Vector2.one * (backdrop * 2f));
            }
        }

        /// <summary>Re-reads the spec's symmetry and redraws; call after any document change.</summary>
        public void Refresh()
        {
            try
            {
                _symmetry = MapSymmetry.Parse(_document.Spec.symmetry);
                _error.text = string.Empty;
            }
            catch (ArgumentException exception)
            {
                _symmetry = null;
                _error.text = exception.Message;
            }

            _context.Symmetry = _symmetry;

            OnViewChanged();
        }

        /// <summary>Shows a preview image drawn in memory (the live preview's); the canvas owns it from now on.</summary>
        public void ShowPreview(Texture2D image) => _image.Show(image);

        /// <summary>Zooms and pans to show <paramref name="feature"/>.</summary>
        public void Frame(MapFeatureRef feature)
        {
            _view.Fit(MapFeatureBounds.Measure(_document.Spec, feature));
            OnViewChanged();
        }

        /// <summary>Rings the middle of <paramref name="feature"/> in <paramref name="color"/> until the next <see cref="ClearMark"/>.</summary>
        public void Mark(MapFeatureRef feature, Color color)
        {
            _marker = (MapFeatureBounds.Measure(_document.Spec, feature).center, color);
            _overlay.MarkDirtyRepaint();
        }

        public void ClearMark()
        {
            _marker = null;
            _overlay.MarkDirtyRepaint();
        }

        /// <summary>Zooms to show the whole playable area.</summary>
        public void Fit()
        {
            _view.Fit(PlayableBounds);
            _fitted = _view.Size.x > 0f;
            OnViewChanged();
        }

        private void Paint(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var spec = _document.Spec;
            MapCanvasPainter.DrawGrid(painter, _view, TerrainBounds);
            MapCanvasPainter.DrawBounds(painter, _view, PlayableBounds, TerrainBounds);
            if (_symmetry != null)
            {
                MapCanvasPainter.DrawSector(painter, _view, _symmetry, PlayableBounds);
                MapSectorWarnings.Draw(painter, spec, _view, _symmetry);
            }

            if (_symmetry != null && _ghosts)
            {
                MapFeaturePainter.DrawGhosts(painter, spec, _view, _symmetry, _layers);
            }

            MapFeaturePainter.DrawFeatures(painter, spec, _view, _layers);
            MapFeaturePainter.DrawSelection(painter, spec, _view, _selection.Current, _layers);
            if (_marker.HasValue)
            {
                MapCanvasPainter.DrawMarker(painter, _view, _marker.Value.Position, _marker.Value.Color);
            }

            _tools.Active.Paint(painter);
        }

        private void OnResized()
        {
            _view.Size = contentRect.size;
            if (!_fitted)
            {
                Fit();
                return;
            }

            OnViewChanged();
        }

        private void OnViewChanged()
        {
            var image = _view.ToLocal(PlayableBounds);
            _image.style.left = image.x;
            _image.style.top = image.y;
            _image.style.width = image.width;
            _image.style.height = image.height;
            _overlay.MarkDirtyRepaint();
            ViewChanged?.Invoke();
        }
    }
}
