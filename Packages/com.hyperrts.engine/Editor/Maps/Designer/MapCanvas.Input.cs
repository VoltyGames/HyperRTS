using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    public sealed partial class MapCanvas
    {
        private const float ZoomStep = 1.15f;
        private const int MiddleButton = 2;

        private bool _spaceHeld;
        private bool _panning;
        private Vector2 _lastPointer;

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
            if (_document != null)
            {
                CursorMoved?.Invoke(_view.ToWorld(position));
            }

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
