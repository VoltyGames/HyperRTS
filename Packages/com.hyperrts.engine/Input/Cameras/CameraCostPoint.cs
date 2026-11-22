using System;

namespace HyperRTS.Input.Cameras
{
    /// <summary>A named ground point and yaw in degrees that <see cref="CameraCostProbe"/> looks at.</summary>
    [Serializable]
    public sealed class CameraCostPoint
    {
        public string name;
        public float x;
        public float z;
        public float yaw;
    }
}
