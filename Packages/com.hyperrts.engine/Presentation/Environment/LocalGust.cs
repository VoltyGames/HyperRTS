using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// A radial push through plants and tree canopies, from rotor downwash or a blast, in units of the wind's
    /// strength. Matches one <c>_HyperRTSGusts</c> entry in WindGlobals.hlsl.
    /// </summary>
    public struct LocalGust
    {
        /// <summary>The shader's <c>HYPERRTS_MAX_GUSTS</c>.</summary>
        public const int Max = 8;

        public const float DownwashRadius = 8f;
        public const float DownwashSpread = 0.4f;
        public const float DownwashPush = 1f;

        /// <summary>Seconds a blast's front takes to spread, and the time constant it relaxes over.</summary>
        public const float BlastFront = 0.25f;
        public const float BlastRelax = 0.35f;
        public const float BlastLife = 1.2f;
        public const float BlastPush = 1.4f;

        /// <summary>A blast reaches this many times its splash radius, within these metres.</summary>
        public const float BlastReachPerRadius = 3f;
        public const float MinBlastReach = 6f;
        public const float MaxBlastReach = 30f;

        public float2 Center;
        public float Radius;
        public float Push;

        /// <summary>Blasts only: the radius the front spreads to and seconds since it went off.</summary>
        public float Reach;
        public float Age;

        public readonly bool Expired => Age >= BlastLife;

        public readonly float4 Packed => new(Center, Radius, Push);

        /// <summary>
        /// Rotors spinning at <paramref name="spin"/> (0 to 1), <paramref name="height"/> metres above the ground;
        /// at <paramref name="reach"/> or higher they leave the grass alone.
        /// </summary>
        public static LocalGust Downwash(float2 center, float height, float spin, float reach)
        {
            var near = math.saturate(1f - height / reach);
            return new LocalGust
            {
                Center = center,
                Radius = DownwashRadius + math.max(height, 0f) * DownwashSpread,
                Push = DownwashPush * spin * near,
            };
        }

        public static LocalGust Blast(float2 center, float splashRadius)
        {
            var blast = new LocalGust
            {
                Center = center,
                Reach = math.clamp(splashRadius * BlastReachPerRadius, MinBlastReach, MaxBlastReach),
            };
            blast.Advance(0f);
            return blast;
        }

        /// <summary>The front spreads out fast while the push behind it relaxes.</summary>
        public void Advance(float deltaTime)
        {
            Age += deltaTime;
            Radius = Reach * math.sqrt(math.saturate(math.max(Age, 0.02f) / BlastFront));
            Push = BlastPush * math.exp(-Age / BlastRelax);
        }
    }
}
