using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Effects.Graph;

/// <summary>One change to a layer field: <c>field=value</c> sets it, <c>field*=factor</c> scales it.</summary>
/// <param name="Field">The serialized path inside the layer, such as <c>size.min</c>.</param>
internal readonly record struct RecipeEdit(string Field, string Value, bool Scales)
{
    /// <summary>Edits joined by semicolons, since a value may hold commas; throws on an entry without a field and an equals sign.</summary>
    public static List<RecipeEdit> ParseAll(string text)
    {
        var edits = text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(entry => Parse(entry.Trim())).ToList();
        return edits.Count > 0 ? edits : throw new ArgumentException("HyperRTS: no field=value change given.");
    }

    private static RecipeEdit Parse(string entry)
    {
        var equals = entry.IndexOf('=');
        var scales = equals > 0 && entry[equals - 1] == '*';
        var field = equals > 0 ? entry[..(scales ? equals - 1 : equals)].Trim() : string.Empty;
        if (field.Length == 0)
        {
            throw new ArgumentException($"HyperRTS: '{entry}' is not 'field=value' or 'field*=factor'.");
        }

        return new RecipeEdit(field, entry[(equals + 1)..].Trim(), scales);
    }
}
