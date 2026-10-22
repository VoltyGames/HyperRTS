using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>The antialiasing every camera with <see cref="CameraAntialiasing"/> uses.</summary>
    public static partial class CameraAntialiasingMode
    {
        [AutoStaticsCleanup]
        private static AntialiasingMode _mode = PresentationQuality.Default.Antialiasing;

        [AutoStaticsCleanup]
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
    }
}
