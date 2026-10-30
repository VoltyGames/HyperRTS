using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// A texture sheet of columns by rows frames. Each particle plays from one fraction of the sheet to another over
    /// its life, holds one random frame from that share, or with randomStart starts on a random frame and plays that
    /// share of the sheet over its life, wrapping round.
    /// </summary>
    [Serializable]
    public struct Flipbook
    {
        public int columns;
        public int rows;
        public float from;
        public float to;
        public bool random;
        public bool randomStart;

        public bool IsSet => columns > 0;

        public int Frames => columns * rows;

        /// <summary>Plays from and to over life on a curve, rather than holding or wrapping from a random frame.</summary>
        public bool PlaysOverLife => IsSet && !random && !randomStart;
    }
}
