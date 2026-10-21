using System;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>A plant growing on a <see cref="GrassTile"/> and its mean coverage there, which sizes the GPU buffers.</summary>
    [Serializable]
    public struct GrassTilePlant
    {
        public int plant;
        public float coverage;
    }
}
