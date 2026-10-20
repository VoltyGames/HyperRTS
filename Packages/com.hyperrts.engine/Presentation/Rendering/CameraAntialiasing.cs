using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Keeps this camera's antialiasing on <see cref="CameraAntialiasingMode.Mode"/>; SMAA at high quality.</summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu(HyperRTSMenu.Rig + "Camera Antialiasing")]
    [Icon(HyperRTSIcons.Cameras)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class CameraAntialiasing : PresentationBehaviour
    {
        private UniversalAdditionalCameraData _data;

        private void OnEnable()
        {
            _data = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            CameraAntialiasingMode.Changed += Apply;
            Apply();
        }

        protected override void OnDisabled() => CameraAntialiasingMode.Changed -= Apply;

        private void Apply()
        {
            _data.antialiasing = CameraAntialiasingMode.Mode;
            _data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
