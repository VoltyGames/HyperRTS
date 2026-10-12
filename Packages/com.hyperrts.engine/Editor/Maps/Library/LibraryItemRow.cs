using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>One item in the Library page's item list: its key, file, pictured asset and, for layers, its map colour.</summary>
    public sealed class LibraryItemRow
    {
        public string Key;
        public string File;
        public Object Asset;
        public Color? Swatch;

        /// <summary>The file did not parse; the problems list says why.</summary>
        public bool Broken;
    }
}
