using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace HyperRTS.Editor.Validation
{
    /// <summary>
    /// Finds UI Toolkit references that point nowhere: stylesheet and template links that do not resolve, and UXML
    /// classes that no stylesheet styles and no script names.
    /// </summary>
    public class UIAssetCheck
    {
        private const string DatabaseUrl = "project://database/";

        private static readonly Regex UxmlLink = new("<(?:ui:)?(?:Style|Template)\\b[^>]*?\\bsrc=\"([^\"]*)\"");
        private static readonly Regex SheetImport = new("@import\\s+(?:url\\(\\s*)?[\"']?([^\"')\\s;]+)");
        private static readonly Regex ClassAttribute = new("\\sclass=\"([^\"]*)\"");
        private static readonly Regex Guid = new("guid=([0-9a-fA-F]+)");
        private static readonly Regex Comment = new("/\\*.*?\\*/|<!--.*?-->", RegexOptions.Singleline);
        private static readonly Regex RuleBody = new("\\{[^}]*\\}");
        private static readonly Regex ImportLine = new("@import[^;]*;");
        private static readonly Regex Selector = new("\\.(-?[_a-zA-Z][\\w-]*)");
        private static readonly Regex StringLiteral = new("\"([^\"\\\\\\r\\n]*)\"");

        private readonly HashSet<string> styled = new();
        private readonly HashSet<string> named = new();

        /// <summary>Reads every sheet and script under <paramref name="searchRoots"/> for the classes they define or name.</summary>
        public UIAssetCheck(params string[] searchRoots)
        {
            foreach (var path in Files(searchRoots, "t:StyleSheet t:ThemeStyleSheet", ".uss"))
            {
                var selectors = RuleBody.Replace(ImportLine.Replace(Blank(ReadText(path)), ""), "");
                styled.UnionWith(Selector.Matches(selectors).Select(match => match.Groups[1].Value));
            }

            foreach (var path in Files(searchRoots, "t:MonoScript", ".cs"))
            {
                foreach (Match literal in StringLiteral.Matches(ReadText(path)))
                {
                    named.UnionWith(literal.Groups[1].Value.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        /// <summary>One "file:line: message" per bad link or unknown class in the UXML, USS and TSS files under <paramref name="root"/>.</summary>
        public List<string> Issues(string root)
        {
            var issues = new List<string>();
            foreach (var path in Files(new[] { root }, "t:VisualTreeAsset", ".uxml"))
            {
                var text = Blank(ReadText(path));
                CheckLinks(path, text, UxmlLink, issues);
                CheckClasses(path, text, issues);
            }

            foreach (var path in Files(new[] { root }, "t:StyleSheet t:ThemeStyleSheet", ".uss", ".tss"))
            {
                CheckLinks(path, Blank(ReadText(path)), SheetImport, issues);
            }

            return issues;
        }

        private static void CheckLinks(string file, string text, Regex link, List<string> issues)
        {
            foreach (Match match in link.Matches(text))
            {
                var value = match.Groups[1].Value;
                if (!Resolves(file, value))
                {
                    issues.Add($"{file}:{Line(text, match.Index)}: link '{value}' does not resolve");
                }
            }
        }

        private void CheckClasses(string file, string text, List<string> issues)
        {
            foreach (Match match in ClassAttribute.Matches(text))
            {
                var classes = match.Groups[1].Value.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var name in classes.Where(name => !name.StartsWith("unity-")))
                {
                    if (!styled.Contains(name) && !named.Contains(name))
                    {
                        issues.Add($"{file}:{Line(text, match.Index)}: class '{name}' has no rule and no script names it");
                    }
                }
            }
        }

        private static bool Resolves(string file, string value)
        {
            var builtIn = value.Contains("://") && !value.StartsWith(DatabaseUrl);
            if (builtIn)
            {
                return true;
            }

            var query = value.IndexOf('?');
            var guid = Guid.Match(query < 0 ? "" : value.Substring(query));
            if (guid.Success)
            {
                return AssetDatabase.GUIDToAssetPath(guid.Groups[1].Value).Length > 0;
            }

            var path = query < 0 ? value : value.Substring(0, query);
            return AssetDatabase.AssetPathToGUID(AssetPath(file, path)).Length > 0;
        }

        private static string AssetPath(string file, string link)
        {
            if (link.StartsWith(DatabaseUrl))
            {
                return link.Substring(DatabaseUrl.Length);
            }

            if (link.StartsWith("/"))
            {
                return link.Substring(1);
            }

            var parts = new List<string>(Path.GetDirectoryName(file).Replace('\\', '/').Split('/'));
            foreach (var part in link.Split('/'))
            {
                if (part == "..")
                {
                    parts.RemoveAt(parts.Count - 1);
                }
                else if (part != ".")
                {
                    parts.Add(part);
                }
            }

            return string.Join("/", parts);
        }

        private static IEnumerable<string> Files(string[] roots, string filter, params string[] extensions) =>
            AssetDatabase.FindAssets(filter, roots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => extensions.Any(extension => path.EndsWith(extension)))
                .Distinct();

        // Package assets live outside the project folder, so the disk path comes from the package.
        private static string ReadText(string assetPath)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
            var disk = package == null ? assetPath : package.resolvedPath + assetPath.Substring(package.assetPath.Length);
            return File.ReadAllText(disk);
        }

        // Comments become spaces so line numbers still match the file.
        private static string Blank(string text) =>
            Comment.Replace(text, match => Regex.Replace(match.Value, "[^\\n]", " "));

        private static int Line(string text, int index) => 1 + text.Take(index).Count(character => character == '\n');
    }
}
