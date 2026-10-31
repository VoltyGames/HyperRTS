using System;

namespace HyperRTS.Presentation.Environment.Vegetation.Grass
{
    /// <summary>A plant growing on a grass tile and its mean coverage there, which sizes the GPU buffers.</summary>
    [Serializable]
    public struct GrassTilePlant
    {
        public int plant;
        public float coverage;
    }
}
