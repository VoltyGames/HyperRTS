using System;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Icons;

/// <summary>Puts a Lucide glyph from Glyphs.uss on a VisualElement through its hrts-glyph classes.</summary>
public static class GlyphElements
{
    private const string GlyphClass = "hrts-glyph";
    private const string NamePrefix = GlyphClass + "--";

    /// <summary>A new element drawing <paramref name="glyph"/>.</summary>
    public static VisualElement CreateGlyph(string glyph, PickingMode pickingMode = PickingMode.Position) =>
        new VisualElement { pickingMode = pickingMode }.SetGlyph(glyph);

    extension(VisualElement element)
    {
        /// <summary>Draws <paramref name="glyph"/> on <paramref name="element"/>, replacing the glyph a recycled row held.</summary>
        public VisualElement SetGlyph(string glyph)
        {
            while (TryFindGlyphName(element, out var held))
            {
                element.RemoveFromClassList(held);
            }

            element.AddToClassList(GlyphClass);
            element.AddToClassList(NamePrefix + glyph);
            return element;
        }
    }

    private static bool TryFindGlyphName(VisualElement element, out string found)
    {
        foreach (var name in element.GetClasses())
        {
            if (name.StartsWith(NamePrefix, StringComparison.Ordinal))
            {
                found = name;
                return true;
            }
        }

        found = null;
        return false;
    }
}
