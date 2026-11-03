using UnityEngine;

namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>One field of a horizon row: its crop, shades and extent along the row, and the margin at each end.</summary>
    public struct HorizonField
    {
        /// <summary>Index of the field's crop in <see cref="HorizonFields.crops"/>, and its terrain layer.</summary>
        public int crop;
        public int layer;

        /// <summary>The field's own shade and its block's broad shade, each 0 to 1.</summary>
        public float shade;
        public float block;

        /// <summary>Where the field starts and ends along the row (metres), its fields slid by the row's slide.</summary>
        public float start;
        public float end;

        /// <summary>Margin at the field's start and end: a field margin, or half a track.</summary>
        public float startMargin;
        public float endMargin;

        public float Length => end - start;

        /// <summary>Square metres of margins and tracks in the field, its row giving the margins along its sides.</summary>
        public float MarginArea(HorizonRow row)
        {
            var inner = Mathf.Max(0f, Length - startMargin - endMargin)
                * Mathf.Max(0f, row.Width - row.southMargin - row.northMargin);
            return Length * row.Width - inner;
        }
    }
}
