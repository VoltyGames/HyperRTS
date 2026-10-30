using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>One burst of a layer: how many particles, how long after the Burst event, and how often it repeats.</summary>
    [Serializable]
    public struct EffectBurst
    {
        public float delay;
        public FloatRange count;
        public int repeats;
        public float interval;
    }
}
