using HyperRTS.Client.Interaction;
using UnityEngine;

namespace HyperRTS.Presentation.Cursors
{
    /// <summary>
    /// Animates the hardware cursor from a <see cref="CursorSet"/>. Hardware rather than drawn, so the cursor never lags
    /// the frame rate.
    /// </summary>
    public sealed class CursorDisplay
    {
        private readonly CursorSet _set;
        private CursorAnimation _animation;
        private float _started;
        private Texture2D _shown;

        public CursorDisplay(CursorSet set)
        {
            _set = set;
        }

        /// <summary>Multiplies the set's base size; the closest drawn size is shown.</summary>
        public float Scale { get; set; } = 1f;

        public void Show(CursorKind kind)
        {
            var animation = _set.Animation(kind);
            var now = Time.unscaledTime;
            if (animation != _animation)
            {
                _animation = animation;
                _started = now;
            }

            var drawn = animation.Closest(_set.baseSize * Scale);
            var texture = drawn.frames[animation.FrameAt(now - _started, drawn.frames.Length)];
            if (texture == _shown)
            {
                return;
            }

            _shown = texture;
            Cursor.SetCursor(texture, drawn.hotspot, CursorMode.Auto);
        }
    }
}
