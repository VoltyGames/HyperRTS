using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>How strongly a <c>LoopEffect</c> with a duration streams as it runs out.</summary>
    public static class LoopFade
    {
        public const float Seconds = 2f;

        /// <summary>
        /// 1 until the last <see cref="Seconds"/> (at most half the duration), then down to 0; 1 forever without one.
        /// </summary>
        public static float Strength(float age, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            var fade = Mathf.Min(Seconds, duration * 0.5f);
            return Mathf.Clamp01((duration - age) / fade);
        }
    }
}
