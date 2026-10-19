using System;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The antialiasing every camera with <see cref="CameraAntialiasing"/> uses.</summary>
    public static class CameraAntialiasingMode
    {
        private static AntialiasingMode _mode = PresentationQuality.Default.Antialiasing;

        public static event Action Changed;

        /// <summary>Written by <see cref="PresentationQuality.Apply"/>.</summary>
        public static AntialiasingMode Mode
        {
            get => _mode;
            internal set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                Changed?.Invoke();
            }
        }

        internal static void Reset(AntialiasingMode mode)
        {
            Changed = null;
            _mode = mode;
        }
    }
}
