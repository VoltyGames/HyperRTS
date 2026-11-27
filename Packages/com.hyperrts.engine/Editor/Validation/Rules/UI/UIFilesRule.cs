using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Sets;

namespace HyperRTS.Editor.Validation.Rules.UI;

/// <summary>
/// UXML and USS files under Assets and the engine package link only to assets that exist, name only element
/// types that exist, and use only classes a USS file styles or C# code uses.
/// </summary>
public sealed class UIFilesRule : IAssetRule
{
    public void Check(AssetSet assets, ValidationIssues issues)
    {
        var project = UIProject.Open();
        var files = new Dictionary<string, string>();
        var classNames = new HashSet<string>();
        foreach (var (folder, assetFolder) in Roots())
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".cs"))
                {
                    classNames.UnionWith(UIProject.ClassNamesIn(File.ReadAllText(file)));
                }
                else if (file.EndsWith(".uxml") || file.EndsWith(".uss"))
                {
                    files[assetFolder + "/" + Path.GetRelativePath(folder, file).Replace('\\', '/')] = File.ReadAllText(file);
                }
            }
        }

        project.CodeClassNames = classNames;
        Check(files, project, issues);
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

    private static (string Folder, string AssetFolder)[] Roots()
    {
        var engine = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIFilesRule).Assembly);
        return [("Assets", "Assets"), (engine.resolvedPath, engine.assetPath)];
    }
}
