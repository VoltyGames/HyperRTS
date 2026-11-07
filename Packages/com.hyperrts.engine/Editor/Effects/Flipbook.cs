using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// A texture sheet of columns by rows frames that each particle plays, holds on one random frame, or plays from a
    /// random start.
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
