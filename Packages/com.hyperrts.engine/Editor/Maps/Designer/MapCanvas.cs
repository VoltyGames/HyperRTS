using System;
using System.IO;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Symmetry;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor's map view: the latest preview image under a metre grid, the bounds and sector edges, every
    /// feature with its symmetry ghosts, and the selection's handles. Middle-drag or Space+drag pans, the wheel
    /// zooms to the cursor, F fits the map.
    /// </summary>
    public sealed class MapCanvas : VisualElement
    {
        private const float ZoomStep = 1.15f;
        private const long PreviewPollMilliseconds = 1000;
        private const int MiddleButton = 2;

        private readonly MapSpecDocument _document;
        private readonly MapFeatureSelection _selection;
        private readonly MapCanvasView _view = new();
        private readonly MapSelectTool _tool;
        private readonly VisualElement _image;
        private readonly VisualElement _overlay;
        private readonly Label _scale;
        private readonly Label _error;

        private MapSymmetry _symmetry;
        private Texture2D _preview;
        private long _previewTicks;
        private bool _fitted;
        private bool _spaceHeld;
        private bool _panning;
        private Vector2 _lastPointer;
        private bool _ghosts = true;

        public MapCanvas(MapSpecDocument document, MapFeatureSelection selection)
        {
            _document = document;
            _selection = selection;
            _tool = new MapSelectTool(document, selection, _view);
            AddToClassList("hrts-map-canvas");
            focusable = true;

            _image = AddChild("hrts-map-canvas__image");
            _overlay = AddChild("hrts-map-canvas__overlay");
            _overlay.generateVisualContent = Paint;
            _scale = AddLabel("hrts-map-canvas__scale");
            _error = AddLabel("hrts-map-canvas__error");

            RegisterCallback<GeometryChangedEvent>(_ => OnResized());
            RegisterCallback<DetachFromPanelEvent>(_ => DestroyPreview());
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
                var size = _document.spec.size.ToVector2();
                return new Rect(-size * 0.5f, size);
            }
        }

        private Rect TerrainBounds
        {
            get
            {
                var margin = _document.spec.terrain.margin;
                var playable = PlayableBounds;
                return new Rect(playable.min - Vector2.one * margin, playable.size + Vector2.one * (margin * 2f));
            }
        }

        /// <summary>Re-reads the spec's symmetry and preview and redraws; call after any document change.</summary>
        public void Refresh()
        {
            try
            {
                _symmetry = MapSymmetry.Parse(_document.spec.symmetry);
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
            var spec = _document.spec;
            MapCanvasPainter.DrawGrid(painter, _view, TerrainBounds);
            MapCanvasPainter.DrawBounds(painter, _view, PlayableBounds, TerrainBounds);
            if (_symmetry != null)
            {
                MapCanvasPainter.DrawSector(painter, _view, _symmetry, PlayableBounds);
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

        /// <summary>
        /// Shows the image the map's last preview wrote, reloading it when it changes. Like the report's overview it
        /// covers the playable bounds, north up.
        /// </summary>
        private void LoadPreview()
        {
            var path = MapReport.ImagePath(_document.MapName + MapReport.PreviewSuffix);
            var ticks = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
            if (ticks == _previewTicks)
            {
                return;
            }

            DestroyPreview();
            _previewTicks = ticks;
            if (ticks != 0L)
            {
                _preview = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                _preview.LoadImage(File.ReadAllBytes(path));
            }

            _image.style.backgroundImage = _preview;
        }

        private void DestroyPreview()
        {
            if (_preview != null)
            {
                UnityEngine.Object.DestroyImmediate(_preview);
            }

            _preview = null;
            _previewTicks = 0L;
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
            if (!_panning && evt.button == 0)
            {
                _tool.Down(evt.localPosition);
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
            else
            {
                _tool.Move(position);
            }

            _lastPointer = position;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            this.ReleasePointer(evt.pointerId);
            _panning = false;
            _tool.Up();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    _spaceHeld = true;
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    _tool.DeleteSelected();
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
