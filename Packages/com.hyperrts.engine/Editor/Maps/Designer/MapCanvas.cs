using System;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's map view: the latest preview image under a metre grid, the bounds and sector edges, every
    /// feature with its symmetry ghosts, and the selection's handles. Middle-drag or Space+drag pans, the wheel
    /// zooms to the cursor, F fits the map, and a left click selects the feature under the cursor.
    /// </summary>
    public sealed class MapCanvas : VisualElement
    {
        private const float ZoomStep = 1.15f;
        private const long PreviewPollMilliseconds = 1000;
        private const int MiddleButton = 2;

        private readonly MapSpecDocument _document;
        private readonly MapFeatureSelection _selection;
        private readonly MapCanvasView _view = new();
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

        public MapCanvas(MapSpecDocument document, MapFeatureSelection selection)
        {
            _document = document;
            _selection = selection;
            AddToClassList("hrts-map-canvas");
            focusable = true;

            _image = new MapCanvasBackdrop();
            Add(_image);
            _overlay = AddChild("hrts-map-canvas__overlay");
            _overlay.generateVisualContent = Paint;
            _scale = AddLabel("hrts-map-canvas__scale");
            _error = AddLabel("hrts-map-canvas__error");

            RegisterCallback<GeometryChangedEvent>(_ => OnResized());
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<KeyUpEvent>(OnKeyUp);
            schedule.Execute(LoadPreview).Every(PreviewPollMilliseconds);
            Refresh();
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

        /// <summary>Re-reads the spec's symmetry and preview and redraws; call after any document change.</summary>
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

            LoadPreview();
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

        private VisualElement AddChild(string className)
        {
            var child = new VisualElement { pickingMode = PickingMode.Ignore };
            child.AddToClassList(className);
            Add(child);
            return child;
        }

        private Label AddLabel(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            Add(label);
            return label;
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

        private void LoadPreview() => _image.Poll(_document.MapName);

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
            if (!_panning && evt.button == 0)
            {
                _selection.Set(MapHitTest.FeatureAt(_document.Spec, _view, evt.localPosition));
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

            _lastPointer = position;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            this.ReleasePointer(evt.pointerId);
            _panning = false;
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
                    return;
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
