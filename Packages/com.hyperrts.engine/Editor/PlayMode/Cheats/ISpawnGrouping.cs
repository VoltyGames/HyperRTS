using System.Collections.Generic;

namespace HyperRTS.Editor.PlayMode.Cheats;

/// <summary>A game's way to group the spawn and strike lists of the Cheats window under headings; the picker finds one implementation by type.</summary>
public interface ISpawnGrouping
{
    /// <summary>The headings in display order.</summary>
    IReadOnlyList<string> Headings { get; }

    /// <summary>
    /// The heading, from <see cref="Headings"/>, for the prefab named <paramref name="entityName"/>; null lists it
    /// under Other. It runs as the list opens, so it must not load assets.
    /// </summary>
    string FindHeading(string entityName);
}
