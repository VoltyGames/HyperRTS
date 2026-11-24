using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;

namespace HyperRTS.Editor.Validation.Rules.UI
{
    /// <summary>
    /// UXML and USS files under Assets and the engine package link only to assets that exist, name only element
    /// types that exist, and use only classes a USS file styles or C# code uses.
    /// </summary>
    public sealed class UIFilesRule : IAssetRule
    {
        public void Check(AssetSet assets, ValidationIssues issues)
        {
            var project = UIProject.Open();
            project.CodeClassNames = ReadCodeClassNames();
            Check(ReadFiles(), project, issues);
        }

        /// <summary>Checks files by asset path and text; the part of the rule that needs no Editor state.</summary>
        public void Check(IReadOnlyDictionary<string, string> files, UIProject project, ValidationIssues issues)
        {
            var parsed = files.Select(file => new UIFile(file.Key, file.Value)).ToList();
            var styled = new HashSet<string>(parsed.Where(file => !file.IsLayout).SelectMany(file => file.SelectorClasses()));
            foreach (var file in parsed)
            {
                if (file.IsLayout)
                {
                    new UxmlCheck(file, project, styled, issues).Run();
                }
                else
                {
                    new UssLinkCheck().Run(file, project, issues);
                }
            }
        }

        private static Dictionary<string, string> ReadFiles()
        {
            var files = new Dictionary<string, string>();
            foreach (var (folder, assetFolder) in Roots())
            {
                foreach (var file in Files(folder, ".uxml", ".uss"))
                {
                    files[assetFolder + "/" + Path.GetRelativePath(folder, file).Replace('\\', '/')] = File.ReadAllText(file);
                }
            }

            return files;
        }

        private static HashSet<string> ReadCodeClassNames()
        {
            var names = new HashSet<string>();
            foreach (var (folder, _) in Roots())
            {
                foreach (var file in Files(folder, ".cs"))
                {
                    names.UnionWith(UIProject.ClassNamesIn(File.ReadAllText(file)));
                }
            }

            return names;
        }

        private static (string Folder, string AssetFolder)[] Roots()
        {
            var engine = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIFilesRule).Assembly);
            return new[] { ("Assets", "Assets"), (engine.resolvedPath, engine.assetPath) };
        }

        private static IEnumerable<string> Files(string folder, params string[] extensions) =>
            Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories).Where(file => extensions.Any(file.EndsWith));
    }
}
