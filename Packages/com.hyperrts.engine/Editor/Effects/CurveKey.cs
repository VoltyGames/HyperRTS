using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>One key of a curve over a particle's life, time 0 at birth and 1 at death.</summary>
    [Serializable]
    public struct CurveKey
    {
        public float time;
        public float value;
    }
}
