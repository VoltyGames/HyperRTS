using HyperRTS.Input.Cameras;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>What an <see cref="EffectCapture"/> shoots: the times, the camera's distance, aim height and pitch, and the subfolder.</summary>
    public sealed class EffectCaptureSettings
    {
        public const string DefaultTimes = "0.1,0.3,0.6,1.2,2.5";
        public const float DefaultDistance = 24f;
        public const float DefaultHeight = 2.5f;

        /// <summary>The default camera's pitch at its closest zoom, where effects are seen largest.</summary>
        public static readonly float DefaultPitch = new CameraZoom().pitchAtMin;

        /// <summary>Seconds after the burst, comma separated.</summary>
        public string Times = DefaultTimes;

        /// <summary>Metres from the camera to the aim point.</summary>
        public float Distance = DefaultDistance;

        /// <summary>Metres above the ground the camera aims at.</summary>
        public float Height = DefaultHeight;

        /// <summary>Degrees the camera looks down from the horizon.</summary>
        public float Pitch = DefaultPitch;

        /// <summary>A subfolder of the rig's folder, so a later capture keeps this one's files; empty writes in the rig's folder.</summary>
        public string Label = string.Empty;
    }
}
