using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>One bird quad as <c>HyperRTS/Bird</c> reads it.</summary>
    public struct BirdInstance
    {
        public Matrix4x4 ObjectToWorld;

        /// <summary>Wing pose in the 2x2 flipbook, 0 to 3.</summary>
        public uint Frame;
    }
}
