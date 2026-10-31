using System;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Presentation.HUD;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's map view: the latest preview image under a metre grid, the bounds and sector edges, every
    /// feature with its symmetry ghosts, and the selection's handles. Middle-drag or Space+drag pans, the wheel
    /// zooms to the cursor, F fits the map. The left button and other keys go to the active tool
    /// (<see cref="MapCanvasTools"/>).
    /// </summary>
    [UxmlElement]
    public sealed partial class MapCanvas : VisualElement
    {
        private const float ZoomStep = 1.15f;
        private const int MiddleButton = 2;

        private MapSpecDocument _document;
        private MapFeatureSelection _selection;
        private readonly MapCanvasView _view = new();
        private readonly MapToolContext _context;
        private MapCanvasTools _tools;
        private readonly MapCanvasBackdrop _image;
        private readonly VisualElement _overlay;
        private readonly Label _scale;
        private readonly Label _error;

        private MapSymmetry _symmetry;
        private bool _fitted;
        private bool _spaceHeld;
        private bool _panning;
        private Vector2 _lastPointer;
        private bool _ghosts = true;

        public MapCanvas()
        {
            focusable = true;
            EditorAssets.Layout(typeof(MapCanvas)).CloneTree(this);
            _image = this.Require<MapCanvasBackdrop>("image");
            _overlay = this.Require<VisualElement>("overlay");
            _scale = this.Require<Label>("scale");
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

        /// <summary>
        /// Shows <paramref name="document"/>, fitted, for the tools in <paramref name="toolbox"/> to edit, with
        /// <paramref name="selection"/> drawn and set by them.
        /// </summary>
        public void Bind(MapSpecDocument document, MapFeatureSelection selection, MapToolbox toolbox)
        {
            var first = _document == null;
            _tools?.Active.Cancel();
            _document = document;
            _selection = selection;
            _context.Document = document;
            _context.Selection = selection;
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

        public void Fit()
        {
            _view.Fit(TerrainBounds);
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
                MapFeaturePainter.DrawGhosts(painter, spec, _view, _symmetry);
            }

            MapFeaturePainter.DrawFeatures(painter, spec, _view);
            MapFeaturePainter.DrawSelection(painter, spec, _view, _selection.Current);
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
            _scale.text = $"Grid {MapCanvasPainter.GridStep(_view):0.#} m";
            _overlay.MarkDirtyRepaint();
        }

        private void OnWheel(WheelEvent evt)
        {
            _view.ZoomAt(evt.localMousePosition, evt.delta.y < 0f ? ZoomStep : 1f / ZoomStep);
            OnViewChanged();
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            this.CapturePointer(evt.pointerId);
            _lastPointer = evt.localPosition;
            _panning = evt.button == MiddleButton || _spaceHeld;
            if (!_panning && evt.button == 0 && _document != null)
            {
                _tools.Active.Down(evt.localPosition, evt.modifiers, evt.clickCount);
                _overlay.MarkDirtyRepaint();
            }

            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 position = evt.localPosition;
            if (_panning)
            {
                _view.PanBy(position - _lastPointer);
                OnViewChanged();
            }
            else if (_document != null)
            {
                _tools.Active.Move(position);
                _overlay.MarkDirtyRepaint();
            }

            _lastPointer = position;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            this.ReleasePointer(evt.pointerId);
            _panning = false;
            _tools?.Active.Up();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    _spaceHeld = true;
                    break;
                case KeyCode.F:
                    Fit();
                    break;
                default:
                    if (_tools == null || !_tools.Key(evt.keyCode, evt.modifiers))
                    {
                        return;
                    }

                    break;
            }

            evt.StopPropagation();
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            if (evt.keyCode == KeyCode.Space)
            {
                _spaceHeld = false;
            }
        }
    }
}
