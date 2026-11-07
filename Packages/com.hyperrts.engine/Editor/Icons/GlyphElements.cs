using System;
using System.Linq;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Icons
{
    /// <summary>Puts a Lucide glyph from Glyphs.uss on a VisualElement through its hrts-glyph classes.</summary>
    public static class GlyphElements
    {
        private const string GlyphClass = "hrts-glyph";
        private const string NamePrefix = GlyphClass + "--";

        /// <summary>A new element drawing <paramref name="glyph"/>.</summary>
        public static VisualElement CreateGlyph(string glyph, PickingMode pickingMode = PickingMode.Position) =>
            new VisualElement { pickingMode = pickingMode }.SetGlyph(glyph);

        /// <summary>Draws <paramref name="glyph"/> on <paramref name="element"/>, replacing the glyph a recycled row held.</summary>
        public static VisualElement SetGlyph(this VisualElement element, string glyph)
        {
            foreach (var name in element.GetClasses().Where(name => name.StartsWith(NamePrefix, StringComparison.Ordinal)).ToList())
            {
                element.RemoveFromClassList(name);
            }

            element.AddToClassList(GlyphClass);
            element.AddToClassList(NamePrefix + glyph);
            return element;
        }
    }
}
