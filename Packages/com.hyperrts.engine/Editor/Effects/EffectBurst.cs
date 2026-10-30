using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>One burst of a layer: how many particles, how long after the Burst event.</summary>
    [Serializable]
    public struct EffectBurst
    {
        public float delay;
        public FloatRange count;
    }
}
