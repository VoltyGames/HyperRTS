using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HyperRTS.Editor.Validation
{
    /// <summary>
    /// Finds namespace-level types without a doc summary, so a code index built from summaries stays readable.
    /// A partial type needs the summary on one of its declarations; folders named Tests are skipped.
    /// </summary>
    public static class TypeSummaries
    {
        // Namespace-level declarations sit at a 4-space indent; nested ones deeper.
        private static readonly Regex TopLevelType =
            new(@"^ {4}(?:\w+ )*(?:class|struct|enum|interface|record) (\w+)");

        /// <summary>"path: Type" for each undocumented type under <paramref name="root"/>, sorted.</summary>
        public static List<string> Missing(string root)
        {
            var documented = new HashSet<string>();
            var undocumented = new Dictionary<string, string>();
            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (InTestFolder(root, path))
                {
                    continue;
                }

                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i++)
                {
                    var match = TopLevelType.Match(lines[i]);
                    if (!match.Success)
                    {
                        continue;
                    }

                    // Partial declarations share a folder, so folder and name identify one type.
                    var key = Path.GetDirectoryName(path) + "|" + match.Groups[1].Value;
                    if (HasSummary(lines, i))
                    {
                        documented.Add(key);
                        continue;
                    }

                    undocumented.TryAdd(key, $"{Relative(root, path)}: {match.Groups[1].Value}");
                }
            }

            return undocumented.Where(entry => !documented.Contains(entry.Key)).Select(entry => entry.Value)
                .OrderBy(entry => entry).ToList();
        }

        // Attributes and plain comments may sit between a summary and its declaration; an index reads only a closed
        // <summary>, so an unclosed one counts as none.
        private static bool HasSummary(string[] lines, int declaration)
        {
            var i = declaration - 1;
            while (i >= 0 && IsAttributeOrComment(lines[i].Trim()))
            {
                i--;
            }

            var doc = new List<string>();
            for (; i >= 0 && lines[i].TrimStart().StartsWith("///"); i--)
            {
                doc.Add(lines[i]);
            }

            var text = string.Concat(doc);
            return text.Contains("<summary>") && text.Contains("</summary>");
        }

        private static bool IsAttributeOrComment(string line)
        {
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                return true;
            }

            return line.StartsWith("//") && !line.StartsWith("///");
        }

        private static bool InTestFolder(string root, string path) =>
            Relative(root, path).Split('/').Contains("Tests");

        // Path.GetRelativePath is missing from the .NET Framework profile editor code may compile against.
        private static string Relative(string root, string path) =>
            Path.GetFullPath(path).Substring(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length + 1)
                .Replace(Path.DirectorySeparatorChar, '/');
    }
}
