namespace HyperRTS.Presentation.Common
{
    /// <summary>Turns 32-bit hashes into the random numbers effects, fires and plants draw from.</summary>
    public static class Hashes
    {
        /// <summary>A hash as a number from 0 up to but not including 1, from its top 24 bits.</summary>
        public static float Fraction(uint hash) => (hash >> 8) * (1f / (1 << 24));
    }
}
