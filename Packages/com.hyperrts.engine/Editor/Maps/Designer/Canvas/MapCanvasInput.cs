using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer.Canvas
{
    /// <summary>The canvas's pointer and key input: pans, zooms and fits the view, and hands the rest to the active tool.</summary>
    internal sealed class MapCanvasInput
    {
        private const float ZoomStep = 1.15f;
        private const int MiddleButton = 2;

        private readonly MapCanvas _canvas;
        private readonly MapCanvasView _view;
        private readonly VisualElement _overlay;
        private bool _spaceHeld;
        private bool _panning;
        private Vector2 _lastPointer;

        public MapCanvasInput(MapCanvas canvas, MapCanvasView view, VisualElement overlay)
        {
            (_canvas, _view, _overlay) = (canvas, view, overlay);
            canvas.RegisterCallback<WheelEvent>(OnWheel);
            canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            canvas.RegisterCallback<KeyDownEvent>(OnKeyDown);
            canvas.RegisterCallback<KeyUpEvent>(OnKeyUp);
        }

        private bool HasTools => _canvas.Tools != null;

        private void OnWheel(WheelEvent evt)
        {
            _view.ZoomAt(evt.localMousePosition, evt.delta.y < 0f ? ZoomStep : 1f / ZoomStep);
            _canvas.OnViewChanged();
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _canvas.Focus();
            _canvas.CapturePointer(evt.pointerId);
            _lastPointer = evt.localPosition;
            _panning = evt.button == MiddleButton || _spaceHeld;
            if (!_panning && evt.button == 0 && HasTools)
            {
                _canvas.Tools.Active.Down(evt.localPosition, evt.modifiers, evt.clickCount);
                _overlay.MarkDirtyRepaint();
            }

            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 position = evt.localPosition;
            if (HasTools)
            {
                _canvas.MoveCursor(_view.ToWorld(position));
            }

            if (_panning)
            {
                _view.PanBy(position - _lastPointer);
                _canvas.OnViewChanged();
            }
            else if (HasTools)
            {
                _canvas.Tools.Active.Move(position);
                _overlay.MarkDirtyRepaint();
            }

            _lastPointer = position;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            _canvas.ReleasePointer(evt.pointerId);
            _panning = false;
            _canvas.Tools?.Active.Up();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    _spaceHeld = true;
                    break;
                case KeyCode.F:
                    _canvas.Fit();
                    break;
                default:
                    if (_canvas.Tools == null || !_canvas.Tools.Key(evt.keyCode, evt.modifiers))
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
