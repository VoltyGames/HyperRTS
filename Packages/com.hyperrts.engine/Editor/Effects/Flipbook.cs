using System;

namespace HyperRTS.Editor.Effects
{
    /// <summary>
    /// A texture sheet of columns by rows frames. Each particle plays from one fraction of the sheet to another over
    /// its life, or holds one random frame from that share.
    /// </summary>
    [Serializable]
    public struct Flipbook
    {
        public int columns;
        public int rows;
        public float from;
        public float to;
        public bool random;

        public bool IsSet => columns > 0;

        public int Frames => columns * rows;
    }
}
