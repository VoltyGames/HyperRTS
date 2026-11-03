namespace HyperRTS.Presentation.Environment.Horizon
{
    /// <summary>One row of the horizon's fields, or one of the two a split grid row makes. Metres in the grid.</summary>
    public struct HorizonRow
    {
        /// <summary>How far along the row its fields are slid, under one cell's length.</summary>
        public float slide;

        /// <summary>Where the row starts and ends across the grid.</summary>
        public float start;
        public float end;

        /// <summary>Margin along the row's south and north edges: a field margin, or half a track.</summary>
        public float southMargin;
        public float northMargin;

        public float Width => end - start;
    }
}
