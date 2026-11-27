using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace HyperRTS.Editor.Maps.Spec;

/// <summary>
/// The one layout map specs are written in, so saves diff cleanly. Reads the text as a document, which keeps
/// member order and every string and number as written.
/// </summary>
public static class JsonFormat
{
    private const string Indent = "  ";
    private const int MaxInlineArray = 100;
    private const int MaxInlinePoint = 4;

    /// <summary>Lays out any JSON text; malformed or trailing text throws a <see cref="FormatException"/>.</summary>
    public static string Format(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Write(document.RootElement);
        }
        catch (JsonException e)
        {
            throw new FormatException($"JSON: {e.Message}", e);
        }
    }

    /// <summary>Lays out a value as a file, ending in a newline.</summary>
    private static string Write(JsonElement element)
    {
        var output = new StringBuilder();
        Append(output, element, 0);
        return output.Append('\n').ToString();
    }

    private static void Append(StringBuilder output, JsonElement element, int depth)
    {
        var inline = Inline(element);
        if (inline != null && Fits(output, element, inline))
        {
            output.Append(inline);
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            AppendObject(output, element, depth);
            return;
        }

        AppendArray(output, element, depth);
    }

    private static void AppendObject(StringBuilder output, JsonElement obj, int depth)
    {
        output.Append('{');
        var properties = obj.EnumerateObject().ToList();
        for (var i = 0; i < properties.Count; i++)
        {
            NewLine(output, depth + 1);
            output.Append(Quote(properties[i].Name)).Append(": ");
            Append(output, properties[i].Value, depth + 1);
            output.Append(i < properties.Count - 1 ? "," : string.Empty);
        }

        NewLine(output, depth);
        output.Append('}');
    }

    private static void AppendArray(StringBuilder output, JsonElement array, int depth)
    {
        output.Append('[');
        var items = array.EnumerateArray().ToList();
        for (var i = 0; i < items.Count; i++)
        {
            NewLine(output, depth + 1);
            Append(output, items[i], depth + 1);
            output.Append(i < items.Count - 1 ? "," : string.Empty);
        }

        NewLine(output, depth);
        output.Append(']');
    }

    /// <summary>The value on one line when it is a scalar, empty, a point or a scalar array; otherwise null.</summary>
    private static string Inline(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return element.GetPropertyCount() == 0 ? "{}" : InlinePoint(element);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return Scalar(element);
        }

        var items = element.EnumerateArray().ToList();
        if (items.Count == 0)
        {
            return "[]";
        }

        var scalars = items.All(item => item.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array));
        return scalars ? "[" + string.Join(", ", items.Select(Scalar)) + "]" : null;
    }

    private static string InlinePoint(JsonElement obj)
    {
        var properties = obj.EnumerateObject().ToList();
        var numeric = properties.All(property => property.Value.ValueKind == JsonValueKind.Number);
        if (!numeric || properties.Count > MaxInlinePoint)
        {
            return null;
        }

        var members = properties.Select(property => Quote(property.Name) + ": " + Scalar(property.Value));
        return "{ " + string.Join(", ", members) + " }";
    }

    /// <summary>
    /// A scalar's JSON text. Map data is single precision, so a float is written in the shortest form that reads
    /// back as the same float: 20.0 to 20, and JsonUtility's 0.34999999403953552 to 0.35. A number with no point or
    /// exponent keeps its digits.
    /// </summary>
    private static string Scalar(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return Quote(element.GetString());
            case JsonValueKind.Number:
                var text = element.GetRawText();
                if (text.AsSpan().IndexOfAny('.', 'e', 'E') >= 0)
                {
                    return ((float)double.Parse(text, CultureInfo.InvariantCulture)).ToString("R", CultureInfo.InvariantCulture);
                }

                return text == "-0" ? "0" : text;
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            default:
                return "null";
        }
    }

    /// <summary>A string as JSON, escaping only what JSON requires, plus the line separators U+0085, U+2028 and U+2029.</summary>
    private static string Quote(string text)
    {
        var quoted = new StringBuilder(text.Length + 2).Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': quoted.Append("\\\""); break;
                case '\\': quoted.Append("\\\\"); break;
                case '\n': quoted.Append("\\n"); break;
                case '\r': quoted.Append("\\r"); break;
                case '\t': quoted.Append("\\t"); break;
                case '\b': quoted.Append("\\b"); break;
                case '\f': quoted.Append("\\f"); break;
                case < (char)32 or (char)0x85 or (char)0x2028 or (char)0x2029:
                    quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default: quoted.Append(c); break;
            }
        }

        return quoted.Append('"').ToString();
    }

    /// <summary>Scalars, points and empty containers always stay inline; scalar arrays only while the line is short.</summary>
    private static bool Fits(StringBuilder output, JsonElement element, string inline)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        return output.Length - LineStart(output) + inline.Length <= MaxInlineArray;
    }

    private static void NewLine(StringBuilder output, int depth)
    {
        output.Append('\n');
        for (var i = 0; i < depth; i++)
        {
            output.Append(Indent);
        }
    }

    private static int LineStart(StringBuilder output)
    {
        for (var i = output.Length - 1; i >= 0; i--)
        {
            if (output[i] == '\n')
            {
                return i + 1;
            }
        }

        return 0;
    }
}
