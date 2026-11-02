namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// Scatter.hlsl's band edges in C#: each plant picks its distance band at its own range, off by up to half the
    /// edge width either way, so neighbouring bands mix over a stretch instead of meeting at a line.
    /// </summary>
    public static class ScatterBands
    {
        /// <summary>Share of a plant's range the stretch where two bands mix spans (Scatter.hlsl's BAND_EDGE_WIDTH).</summary>
        public const float EdgeWidth = 0.4f;

        /// <summary>The nearest a plant can stand and still fall past <paramref name="edge"/>.</summary>
        public static float FirstPast(float edge) => edge / (1f + EdgeWidth * 0.5f);

        /// <summary>The farthest a plant can stand and still fall short of <paramref name="edge"/>.</summary>
        public static float LastShort(float edge) => edge / (1f - EdgeWidth * 0.5f);
    }
}
