using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>A value picked per particle between min and max; equal ends make it constant.</summary>
    [Serializable]
    public struct FloatRange
    {
        public float min;
        public float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public bool IsConstant => min == max;
    }
}
