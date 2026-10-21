using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>When a unit with an under fire voice was last hit, so it calls out only after a quiet spell.</summary>
    public struct UnderFireVoice : IComponentData
    {
        /// <summary>Seconds without a hit before the next one calls out again.</summary>
        public const float QuietTime = 10f;

        public double LastHit;
        public bool WasHit;

        /// <summary>Records a hit; true when it broke a quiet spell.</summary>
        public bool Strike(double now)
        {
            var quiet = !WasHit || now - LastHit >= QuietTime;
            WasHit = true;
            LastHit = now;
            return quiet;
        }
    }
}
