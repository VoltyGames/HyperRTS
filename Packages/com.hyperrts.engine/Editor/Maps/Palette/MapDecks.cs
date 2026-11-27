using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Palette;

/// <summary>Every bridge's <see cref="MapDeck"/> in a palette, read on the main thread so planning never touches a prefab.</summary>
public sealed class MapDecks
{
    private readonly Dictionary<string, MapDeck> _decks = new();

    private MapDecks()
    {
    }

    public static MapDecks Read(MapPalette palette)
    {
        var decks = new MapDecks();
        foreach (var entry in palette.entries)
        {
            if (entry.HasDeck)
            {
                decks._decks[entry.key] = MapDeck.Read(entry);
            }
        }

        return decks;
    }

    /// <summary>The deck of the bridge <paramref name="prop"/>; throws for a prop that is not a bridge.</summary>
    public MapDeck Find(string prop) =>
        _decks.TryGetValue(prop, out var deck) ? deck : throw new ArgumentException($"HyperRTS: '{prop}' is not a bridge in the palette.");
}
