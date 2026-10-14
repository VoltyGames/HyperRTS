using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The game's antialiasing, applied to every camera with <see cref="CameraAntialiasing"/>.</summary>
    public static class CameraAntialiasingMode
    {
        private static AntialiasingMode _mode = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        public static event Action Changed;

        /// <summary>Set by the game from its graphics options.</summary>
        public static AntialiasingMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                Changed?.Invoke();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
            _mode = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }
    }
}
