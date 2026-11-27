using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HyperRTS.Editor.Validation.Issues;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Validation.Rules.UI;

using Match = System.Text.RegularExpressions.Match;

/// <summary>
/// Checks one UXML file: <c>Style</c> and <c>Template</c> links that name no asset, custom element tags whose
/// type is gone, and classes no style sheet styles (a warning, unless a C# file uses the class).
/// </summary>
internal sealed class UxmlCheck(UIFile file, UIProject project, HashSet<string> styled, ValidationIssues issues)
{
    // Unity's own classes live in its built-in sheets, which this project does not hold.
    private const string UnityClassPrefix = "unity-";

    private static readonly Regex TagPattern = new(@"<(?![/?!])([\w:.]+)((?:""[^""]*""|'[^']*'|[^>""'])*)>");
    private static readonly Regex AttributePattern = new(@"([\w:.-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')");
    [NoAutoStaticsCleanup]
    private static readonly HashSet<string> Structural = ["UXML", "Style", "Template", "Instance", "AttributeOverrides"];

    private readonly UIFile _file = file;
    private readonly UIProject _project = project;
    private readonly HashSet<string> _styled = styled;
    private readonly HashSet<string> _reportedClasses = [];
    private readonly Dictionary<string, string> _namespaces = [];

    public void Run()
    {
        var tags = new List<(string Name, int Index, List<(string Key, string Value, int Index)> Attributes)>();
        foreach (Match tag in TagPattern.Matches(_file.Code))
        {
            tags.Add((tag.Groups[1].Value, tag.Index, ReadAttributes(tag.Groups[2])));
        }

        foreach (var (_, _, attributes) in tags)
        {
            foreach (var (key, value, _) in attributes)
            {
                if (key == "xmlns" || key.StartsWith("xmlns:"))
                {
                    _namespaces[key.Length > 6 ? key[6..] : ""] = value;
                }
            }
        }

        foreach (var (name, index, attributes) in tags)
        {
            var colon = name.IndexOf(':');
            var local = name[(colon + 1)..];
            CheckElement(colon < 0 ? "" : name[..colon], local, index);
            foreach (var (key, value, at) in attributes)
            {
                if (key == "src" && (local == "Style" || local == "Template"))
                {
                    CheckLink(local, value, at);
                }
                else if (key == "class")
                {
                    CheckClasses(value, at);
                }
            }
        }
    }

    private static List<(string, string, int)> ReadAttributes(Group text)
    {
        var attributes = new List<(string, string, int)>();
        foreach (Match attribute in AttributePattern.Matches(text.Value))
        {
            var value = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Value;
            attributes.Add((attribute.Groups[1].Value, value, text.Index + attribute.Index));
        }

        return attributes;
    }

    private void CheckLink(string tag, string src, int index)
    {
        var target = _file.Resolve(src);
        if (target != null && !_project.AssetExists(target))
        {
            Report(true, index, $"<{tag}> src '{src}' names no asset.");
        }
    }

    private void CheckElement(string prefix, string local, int index)
    {
        if (Structural.Contains(local) || !_namespaces.TryGetValue(prefix, out var space))
        {
            return;
        }

        // A namespace names a C# namespace; a schema URL is not a place to look for types.
        if (space.Contains(':') || space.Contains('/') || _project.ElementExists(space + "." + local))
        {
            return;
        }

        Report(true, index, $"the element <{(prefix == "" ? "" : prefix + ":")}{local}> has no UXML element type '{space}.{local}'.");
    }

    private void CheckClasses(string value, int index)
    {
        foreach (var name in value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!name.StartsWith(UnityClassPrefix) && !_styled.Contains(name) && !_project.CodeClassNames.Contains(name) && _reportedClasses.Add(name))
            {
                Report(false, index, $"the class '{name}' is styled by no USS file.");
            }
        }
    }

    private void Report(bool error, int index, string message)
    {
        var text = $"{_file.Path}:{_file.LineAt(index)}: {message}";
        var context = _project.AssetAt(_file.Path);
        if (error)
        {
            issues.Error(context, text);
        }
        else
        {
            issues.Warn(context, text);
        }
    }
}
