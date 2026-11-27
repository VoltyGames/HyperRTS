using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Validation.Rules.UI;

using Match = System.Text.RegularExpressions.Match;

/// <summary>What the UI file rule asks the project: whether an asset or an element type exists, and the asset to point an issue at.</summary>
public sealed class UIProject
{
    public Func<string, bool> AssetExists;
    public Func<string, bool> ElementExists;
    public Func<string, Object> AssetAt;

    /// <summary>The class names the project's C# uses, so a class only code styles or queries counts as used.</summary>
    public HashSet<string> CodeClassNames = [];

    private static readonly Regex ClassName = new("""
          (?:AddToClassList|RemoveFromClassList|EnableInClassList|ClassListContains|ToggleInClassList)\(\s*"([^"]+)"
        | \bclassName\s*:\s*"([^"]+)"
        | \.(?:Q|Query)(?:<[^>]*>)?\([^)",]*(?:"[^"]*")?\s*,\s*"([^"]+)"
        | \bconst\s+string\s+\w*Class(?:Name)?\s*=\s*"([^"]+)"
        """, RegexOptions.IgnorePatternWhitespace);

    /// <summary>The class names a C# file passes to class list calls and queries, or holds in a const named for a class.</summary>
    public static IEnumerable<string> ClassNamesIn(string code) =>
        ClassName.Matches(code).Select(FirstGroup);

    /// <summary>The text of the first capture group that matched, or empty when none did.</summary>
    public static string FirstGroup(Match match)
    {
        for (var i = 1; i < match.Groups.Count; i++)
        {
            if (match.Groups[i].Success)
            {
                return match.Groups[i].Value;
            }
        }

        return "";
    }

    /// <summary>The open project: its asset database and every loaded type UXML can name.</summary>
    public static UIProject Open()
    {
        // Tags also name bindings and UXML objects, and the cache lists derived types without the base itself.
        var elements = TypeCache.GetTypesDerivedFrom<VisualElement>()
            .Concat(TypeCache.GetTypesDerivedFrom<Binding>())
            .Concat(TypeCache.GetTypesWithAttribute<UxmlObjectAttribute>())
            .Append(typeof(VisualElement)).Select(type => type.FullName).ToHashSet();
        return new UIProject
        {
            AssetExists = path => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)),
            ElementExists = elements.Contains,
            AssetAt = AssetDatabase.LoadMainAssetAtPath,
        };
    }
}
