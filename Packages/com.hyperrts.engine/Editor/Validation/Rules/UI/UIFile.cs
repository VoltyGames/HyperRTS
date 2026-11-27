using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HyperRTS.Editor.Validation.Rules.UI;

using Match = System.Text.RegularExpressions.Match;

/// <summary>One UXML or USS file: its comment-free text, line numbers and the asset paths its links name.</summary>
internal sealed class UIFile
{
    private static readonly Regex XmlComment = new(@"<!--.*?-->", RegexOptions.Singleline);
    private static readonly Regex CssComment = new(@"/\*.*?\*/", RegexOptions.Singleline);
    private static readonly Regex Quoted = new("""
        "[^"]*"|'[^']*'|url\([^)]*\)
        """);
    private static readonly Regex Selector = new(@"\.(-?[_a-zA-Z][\w-]*)");

    public readonly string Path;

    /// <summary>The text with comments blanked, so indexes and line numbers still match the file.</summary>
    public readonly string Code;

    private readonly List<int> _lineStarts = [0];

    public UIFile(string path, string text)
    {
        Path = path;
        Code = Blank(path.EndsWith(".uxml") ? XmlComment : CssComment, text);
        for (var i = 0; i < Code.Length; i++)
        {
            if (Code[i] == '\n')
            {
                _lineStarts.Add(i + 1);
            }
        }
    }

    public bool IsLayout => Path.EndsWith(".uxml");

    public int LineAt(int index)
    {
        var found = _lineStarts.BinarySearch(index);
        return found >= 0 ? found + 1 : ~found;
    }

    /// <summary>Every class a selector in this style sheet names, compound selectors included.</summary>
    public IEnumerable<string> SelectorClasses()
    {
        foreach (Match match in Selector.Matches(Blank(Quoted, Code)))
        {
            yield return match.Groups[1].Value;
        }
    }

    /// <summary>The asset path a link names, or null for a scheme this check does not follow.</summary>
    public string Resolve(string link)
    {
        link = link.Trim();
        var cut = link.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            link = link[..cut];
        }

        const string database = "project://database/";
        if (link.StartsWith(database))
        {
            link = "/" + link[database.Length..];
        }

        if (link.StartsWith("data:") || link.Contains("://"))
        {
            return null;
        }

        link = Uri.UnescapeDataString(link);
        var folder = link.StartsWith("/") ? "" : Path[..(Path.LastIndexOf('/') + 1)];
        return Normalize(folder + link.TrimStart('/'));
    }

    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part == ".." && parts.Count > 0)
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (part != "." && part != "")
            {
                parts.Add(part);
            }
        }

        return string.Join("/", parts);
    }

    private static string Blank(Regex pattern, string text) =>
        pattern.Replace(text, match => Regex.Replace(match.Value, @"[^\n]", " "));
}
